using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// What every command that changes a record's KPIs does first: resolve the record, refuse a caller who may not manage
/// its KPIs, load the KPI, and name the actor and the moment. Then it saves, reporting a collision as a conflict.
/// </summary>
/// <remarks>
/// One path so every command answers the same way: a record the caller cannot read and a KPI on another record are
/// both <see cref="KpiAccessErrors.NotFound"/>. Registered by Infrastructure; public because Wolverine's generated
/// handlers construct it.
/// </remarks>
public sealed class KpiCommandScope(
    KpiSubjectAccessResolver accessResolver,
    IKpisDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ISettings<GeneralSettings> generalSettings)
{
    private readonly KpiSubjectAccessResolver _accessResolver = accessResolver;
    private readonly IKpisDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ISettings<GeneralSettings> _generalSettings = generalSettings;

    /// <summary>The KPI tables.</summary>
    public IKpisDbContext DbContext => _dbContext;

    /// <summary>The caller, as KPI events record them.</summary>
    public EventActor Actor => EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());

    /// <summary>The caller's linked employee, when there is one.</summary>
    public Guid? EmployeeId => _currentUser.GetEmployeeId();

    /// <summary>Now.</summary>
    public Instant Now => _dateTimeProvider.Now;

    /// <summary>
    /// The system time zone, which a measurement's calendar days are read in: the day a point-in-time moment falls on,
    /// and the day a value was taken when the caller does not say.
    /// </summary>
    public async Task<DateTimeZone> SystemZone(CancellationToken cancellationToken) =>
        (await _generalSettings.Get(cancellationToken)).Zone();

    /// <summary>
    /// The record, when it exists, the caller can read it and may manage its KPIs.
    /// </summary>
    public async Task<Result<ResolvedKpiSubject>> ForManaging(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(subjectType, subjectId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<ResolvedKpiSubject>(resolved.Error);

        if (resolved.Value is not { } subject)
            return Result.Failure<ResolvedKpiSubject>(KpiAccessErrors.NotFound);

        return subject.Access.CanManage
            ? subject
            : Result.Failure<ResolvedKpiSubject>(KpiAccessErrors.CannotManage);
    }

    /// <summary>
    /// One of the record's KPIs, tracked, when the caller may manage the record's KPIs.
    /// </summary>
    /// <param name="withMeasurements">
    /// Load the measurements too. Every measurement change and window change needs them: the clash rules and the
    /// window lock check the loaded ones only.
    /// </param>
    public async Task<Result<Kpi>> KpiForManaging(string subjectType, Guid subjectId, Guid kpiId, bool withMeasurements, CancellationToken cancellationToken)
    {
        var subject = await ForManaging(subjectType, subjectId, cancellationToken);
        if (subject.IsFailure)
            return Result.Failure<Kpi>(subject.Error);

        var query = _dbContext.Kpis.AsQueryable();
        if (withMeasurements)
            query = query.Include(k => k.Measurements);

        var kpi = await query.FirstOrDefaultAsync(
            k => k.Id == kpiId && k.SubjectType == subject.Value.SubjectType && k.SubjectId == subjectId,
            cancellationToken);

        return kpi is null ? Result.Failure<Kpi>(KpiAccessErrors.NotFound) : kpi;
    }

    /// <summary>Refuses a caller who may not manage the organization's KPI categories.</summary>
    public async Task<Result> ForManagingCategories(CancellationToken cancellationToken) =>
        await _accessResolver.CanManageCategories(cancellationToken)
            ? Result.Success()
            : Result.Failure(KpiAccessErrors.CannotManageCategories);

    /// <summary>
    /// Confirms a category exists and is active before it is given to a KPI. Null passes: a KPI may be uncategorized.
    /// </summary>
    public async Task<Result> CheckCategoryAssignable(Guid? categoryId, Guid? currentCategoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not { } id || id == currentCategoryId)
            return Result.Success();

        var category = await _dbContext.KpiCategories
            .Where(c => c.Id == id)
            .Select(c => new { c.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        // Not NotFound: that is a 404, which reads as the KPI or its record being gone.
        if (category is null)
            return Result.Failure(KpiAccessErrors.CategoryNotFound);

        return category.IsActive ? Result.Success() : Result.Failure(KpiAccessErrors.CategoryInactive);
    }

    /// <summary>
    /// Saves a change to the KPI's measurements, writing the KPI's row too so a concurrent change to the same KPI's
    /// measurements collides instead of both passing the overlap rule.
    /// </summary>
    public Task<Result> SaveMeasurements(Kpi kpi, CancellationToken cancellationToken)
    {
        _dbContext.TouchKpi(kpi);
        return Save(cancellationToken);
    }

    /// <summary>
    /// Saves, reporting a collision with another change — a row version, a unique index, or a foreign key another save
    /// changed the other end of — as a conflict. Any other failed save is thrown: it is not the caller's to retry.
    /// </summary>
    public async Task<Result> Save(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsCollision(ex))
        {
            return Result.Failure(KpiAccessErrors.Conflict);
        }
    }

    // SQL Server: 2601 = unique index, 2627 = unique constraint, 547 = foreign key — a category deleted while a KPI
    // was being given it, or given a KPI while being deleted, after each request's own check passed. Read without
    // naming SqlException, which the application layers do not reference.
    private static bool IsCollision(DbUpdateException exception) =>
        exception.InnerException?.GetType().GetProperty("Number")?.GetValue(exception.InnerException) is 2601 or 2627 or 547;
}
