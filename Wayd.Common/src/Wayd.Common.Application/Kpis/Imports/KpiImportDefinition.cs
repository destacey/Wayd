using System.Globalization;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Imports;

/// <summary>
/// Imports KPIs onto existing records, each with the history its measurements file gives it — what a customer
/// brings from a spreadsheet when they start on Wayd.
/// </summary>
/// <remarks>
/// Per row: a KPI and its measurements are one aggregate and apply together, while one team's bad row leaves
/// every other team's KPIs to import. A row arriving closed keeps its history and stays off the scorecard.
/// <para>
/// Gated on the KPI administrator permission rather than on each record's own rule: an import is the
/// onboarding of a whole organization's history, not one team's change, and its pass may run after the
/// request that submitted it has gone, when there is no caller to ask about membership.
/// </para>
/// </remarks>
public sealed class KpiImportDefinition(
    IKpisDbContext dbContext,
    IEnumerable<IKpiSubjectType> subjectTypes,
    IFeatureFlags featureFlags,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ISystemCalendar systemCalendar,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportKpiDto>(serializer)
{
    public const string ImportKey = "kpis.kpis";

    private readonly IKpisDbContext _dbContext = dbContext;
    private readonly IEnumerable<IKpiSubjectType> _subjectTypes = subjectTypes;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    public override string Key => ImportKey;
    public override string DisplayName => "KPIs";
    public override string PermissionAction => ApplicationAction.Administer;
    public override string PermissionResource => ApplicationResource.Kpis;

    /// <summary>
    /// The cap counts KPIs; their measurements are not counted. A year of weekly history for every KPI of a
    /// few hundred teams fits well within it.
    /// </summary>
    public override int MaxRows => 10_000;

    /// <summary>Smaller than the default chunk: each row carries its whole history.</summary>
    public override int ChunkSize => 100;

    public override Task<bool> IsEnabled(CancellationToken cancellationToken) =>
        _featureFlags.IsEnabled(FeatureFlags.Names.Kpis, cancellationToken);

    protected override IReadOnlyList<ImportPass<ImportKpiDto>> Steps =>
    [
        new("CreateKpis", ImportPassScope.Chunked, CreateKpis),
    ];

    /// <summary>Links each KPI's row to the record it was added to, since a KPI has no page of its own.</summary>
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken)
    {
        var kpis = await _dbContext.Kpis
            .AsNoTracking()
            .Where(k => createdEntityIds.Contains(k.Id))
            .Select(k => new { k.Id, k.SubjectType, k.SubjectId })
            .ToListAsync(cancellationToken);

        var links = new Dictionary<Guid, ImportRecordLink>();
        foreach (var group in kpis.GroupBy(k => k.SubjectType, StringComparer.OrdinalIgnoreCase))
        {
            var subjects = await DescribeSubjects(group.Key, group.Select(k => k.SubjectId).Distinct().ToList(), cancellationToken);
            foreach (var kpi in group)
            {
                if (subjects.TryGetValue(kpi.SubjectId, out var link))
                    links[kpi.Id] = link;
            }
        }

        return links;
    }

    private async Task<Result> CreateKpis(ImportPassContext<ImportKpiDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;
        var actor = EventActor.Import(_currentUser.GetUserId());
        var zone = await _systemCalendar.Zone(cancellationToken);

        var subjects = await ResolveSubjects(context, cancellationToken);
        var categories = await ResolveCategories(context, cancellationToken);
        var nextOrder = await ResolveNextOrders(subjects.Values.SelectMany(ids => ids), cancellationToken);
        var taken = await ResolveTakenNames(subjects.Values.SelectMany(ids => ids), cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            var descriptor = KpiSubjectTypes.Resolve(data.SubjectType);
            if (descriptor.IsFailure)
            {
                row.Failed(descriptor.Error);
                continue;
            }

            if (!subjects.TryGetValue(descriptor.Value.Key, out var known) || !known.Contains(data.SubjectId))
            {
                row.Failed($"No {descriptor.Value.DisplayName.ToLowerInvariant()} was found with id '{data.SubjectId}'.");
                continue;
            }

            if (data.CategoryId is { } categoryId && !categories.Contains(categoryId))
            {
                row.Failed($"No active KPI category was found with id '{categoryId}'.");
                continue;
            }

            // The name is the natural key a re-submitted file is matched on: without it, fixing one failed row and
            // sending the files again would add every KPI that already imported a second time.
            var nameKey = NameKey(descriptor.Value.Key, data.SubjectId, data.Name);
            if (taken.Contains(nameKey))
            {
                row.Failed($"A KPI named '{data.Name.Trim()}' already exists on this {descriptor.Value.DisplayName.ToLowerInvariant()}, or appears twice in the file.");
                continue;
            }

            var order = nextOrder.GetValueOrDefault(data.SubjectId);
            var goal = new KpiGoal(data.Direction, data.Baseline, data.Target, data.AtRisk);
            var definition = new KpiDefinition(
                data.Name, data.Description, data.CategoryId, data.Prefix, data.Suffix, data.ExpressedAs, goal,
                data.Window, data.RollingAverageOf);

            var created = Kpi.Create(descriptor.Value.Key, data.SubjectId, definition, order, actor, timestamp);
            if (created.IsFailure)
            {
                row.Failed($"Could not add KPI '{data.Name}': {created.Error}");
                continue;
            }

            var kpi = created.Value;
            var history = AddHistory(kpi, data.Measurements, zone, actor, timestamp);
            if (history.IsFailure)
            {
                row.Failed($"Could not add the history of KPI '{data.Name}': {history.Error}");
                continue;
            }

            if (data.IsClosed)
                kpi.Close(actor, timestamp);

            _dbContext.Kpis.Add(kpi);
            taken.Add(nameKey);
            nextOrder[data.SubjectId] = order + 1;
            row.Created(kpi.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Records the history oldest first, so a clash is reported against the later of the two values. Whether each
    /// value says when it was taken the way the KPI's window takes is the KPI's to decide.
    /// </summary>
    private static Result AddHistory(
        Kpi kpi, IReadOnlyList<ImportKpiMeasurementDto> measurements, DateTimeZone zone, EventActor actor, Instant timestamp)
    {
        foreach (var m in measurements.OrderBy(m => m.PeriodStart ?? m.LastDay(zone)).ThenBy(m => m.MeasuredAt))
        {
            if (m.LastDay(zone) is not { } lastDay)
                return Result.Failure("A measurement needs a period, an as-of date or a moment.");

            var input = new KpiMeasurementInput(
                m.Value, m.Time(), null, null, m.MeasuredOn ?? lastDay, null, KpiMeasurementSource.Manual, m.SampleSize, m.Note);

            var added = kpi.AddMeasurement(input, zone, actor, timestamp);
            if (added.IsFailure)
                return Result.Failure($"{added.Error} ({m.Describe()})");
        }

        return Result.Success();
    }

    /// <summary>The records each subject type finds, by the registered spelling of the type.</summary>
    private async Task<Dictionary<string, HashSet<Guid>>> ResolveSubjects(
        ImportPassContext<ImportKpiDto> context, CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);

        var byType = context.Rows
            .Select(r => (Type: KpiSubjectTypes.Resolve(r.Data.SubjectType), r.Data.SubjectId))
            .Where(x => x.Type.IsSuccess)
            .GroupBy(x => x.Type.Value.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byType)
        {
            var found = await DescribeSubjects(group.Key, group.Select(x => x.SubjectId).Distinct().ToList(), cancellationToken);
            resolved[group.Key] = found.Keys.ToHashSet();
        }

        return resolved;
    }

    private async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeSubjects(
        string subjectType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var implementation = _subjectTypes.FirstOrDefault(t =>
            string.Equals(t.SubjectType, subjectType, StringComparison.OrdinalIgnoreCase));

        return implementation is null
            ? new Dictionary<Guid, ImportRecordLink>()
            : await implementation.Describe(ids, cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveCategories(ImportPassContext<ImportKpiDto> context, CancellationToken cancellationToken)
    {
        var ids = context.Rows
            .Where(r => r.Data.CategoryId.HasValue)
            .Select(r => r.Data.CategoryId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return [];

        return (await _dbContext.KpiCategories
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id) && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    /// <summary>
    /// Where each record's next KPI goes: after the ones it already has. Keyed by record id alone, which is
    /// unique across subject types in practice since every record id is a fresh GUID.
    /// </summary>
    /// <summary>The names the records already use, as <see cref="NameKey"/> builds them.</summary>
    private async Task<HashSet<string>> ResolveTakenNames(IEnumerable<Guid> subjectIds, CancellationToken cancellationToken)
    {
        var ids = subjectIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var existing = await _dbContext.Kpis
            .Where(k => ids.Contains(k.SubjectId))
            .Select(k => new { k.SubjectType, k.SubjectId, k.Name })
            .ToListAsync(cancellationToken);

        return existing.Select(k => NameKey(k.SubjectType, k.SubjectId, k.Name)).ToHashSet();
    }

    /// <summary>One KPI name on one record, ignoring case and surrounding whitespace.</summary>
    private static string NameKey(string subjectType, Guid subjectId, string name) =>
        $"{subjectType.ToUpperInvariant()}|{subjectId}|{name.Trim().ToUpperInvariant()}";

    private async Task<Dictionary<Guid, int>> ResolveNextOrders(IEnumerable<Guid> subjectIds, CancellationToken cancellationToken)
    {
        var ids = subjectIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await _dbContext.Kpis
            .Where(k => ids.Contains(k.SubjectId))
            .GroupBy(k => k.SubjectId)
            .Select(g => new { SubjectId = g.Key, Next = g.Max(k => k.Order) + 1 })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Next, cancellationToken);
    }
}
