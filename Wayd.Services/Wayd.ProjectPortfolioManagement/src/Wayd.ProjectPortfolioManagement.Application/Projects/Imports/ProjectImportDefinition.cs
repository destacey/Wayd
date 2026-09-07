using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Imports;

/// <summary>
/// Imports projects into the portfolios that own them, each walked to its target status through the real
/// lifecycle transitions.
/// </summary>
/// <remarks>
/// Atomic, matching the single save the command it replaces did. Projects are what tasks, stages and
/// strategic initiatives are imported against, so a half-applied file leaves those imports resolving only
/// some of the keys they reference.
/// </remarks>
public sealed class ProjectImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProjectDto>(serializer)
{
    public const string ImportKey = "ppm.projects";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Projects";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Projects;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportProjectDto>> Steps =>
    [
        new("CreateProjects", ImportPassScope.WholeSet, CreateProjects),
    ];

    /// <summary>
    /// Resolves every reference and rejects what cannot be found, then creates what is left.
    /// </summary>
    private async Task<Result> CreateProjects(ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var takenKeys = await ResolveTakenKeys(context, cancellationToken);
        var portfoliosById = await ResolvePortfolios(context, cancellationToken);
        var categoryIds = await ResolveExpenditureCategoryIds(context, cancellationToken);
        var lifecyclesById = await ResolveLifecycles(context, cancellationToken);
        var activeThemeIds = await ResolveActiveThemeIds(context, cancellationToken);
        var employeeIdsByNumber = await ResolveEmployees(context, cancellationToken);

        // Projects are ranked at the bottom of their portfolio on creation, so the running max rank per
        // portfolio has to advance as rows are applied — otherwise every imported project is handed the
        // same rank.
        var maxRankByPortfolio = await CurrentMaxRanks(portfoliosById.Keys, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var key = data.Key.Value;

            if (takenKeys.Contains(key))
            {
                row.Failed($"A project already exists with key '{key}'.");
                continue;
            }

            if (!portfoliosById.TryGetValue(data.PortfolioId, out var portfolio))
            {
                row.Failed($"No portfolio was found with id '{data.PortfolioId}'.");
                continue;
            }

            // Programs are scoped to their portfolio, so a program from a different one is as wrong as
            // one that does not exist.
            if (data.ProgramId is { } programId && !portfolio.Programs.Any(p => p.Id == programId))
            {
                row.Failed($"No program was found with id '{programId}' in portfolio '{portfolio.Name}'.");
                continue;
            }

            if (!categoryIds.Contains(data.ExpenditureCategoryId))
            {
                row.Failed($"No expenditure category was found with id '{data.ExpenditureCategoryId}'.");
                continue;
            }

            ProjectLifecycle? lifecycle = null;
            if (data.ProjectLifecycleId is { } lifecycleId && !lifecyclesById.TryGetValue(lifecycleId, out lifecycle))
            {
                row.Failed($"No project lifecycle was found with id '{lifecycleId}'.");
                continue;
            }

            // Only active themes can be attached, so an inactive one is reported rather than dropped.
            var unusableThemes = data.StrategicThemeIds.Where(id => !activeThemeIds.Contains(id)).ToList();
            if (unusableThemes.Count > 0)
            {
                row.Failed($"No active strategic theme was found with id {Quote(unusableThemes.Select(id => id.ToString()))}.");
                continue;
            }

            var unresolvedEmployees = RoleEmployeeNumbers(data)
                .Select(Normalize)
                .Where(n => !employeeIdsByNumber.ContainsKey(n))
                .Distinct()
                .ToList();
            if (unresolvedEmployees.Count > 0)
            {
                row.Failed($"No employee was found with number {Quote(unresolvedEmployees)}.");
                continue;
            }

            var dateRange = data.Start is null || data.End is null
                ? null
                : new LocalDateRange(data.Start.Value, data.End.Value);

            maxRankByPortfolio.TryGetValue(portfolio.Id, out var currentMaxRank);

            var created = portfolio.CreateProject(
                Normalize(data.Name),
                data.Description.Trim(),
                data.Key,
                data.ExpenditureCategoryId,
                dateRange,
                data.ProgramId,
                data.BusinessCase?.Trim(),
                data.ExpectedBenefits?.Trim(),
                BuildRoles(data, employeeIdsByNumber),
                [.. data.StrategicThemeIds],
                At(data.CreatedOn),
                PpmActor.System,
                currentMaxRank);
            if (created.IsFailure)
            {
                row.Failed($"Could not create project '{key}' in portfolio '{portfolio.Name}': {created.Error}");
                continue;
            }

            var project = created.Value;
            maxRankByPortfolio[portfolio.Id] = project.Rank;

            if (lifecycle is not null)
            {
                var assigned = project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle);
                if (assigned.IsFailure)
                {
                    row.Failed($"Could not assign lifecycle '{lifecycle.Name}' to project '{key}': {assigned.Error}");
                    continue;
                }
            }

            var transition = ApplyStatus(project, data);
            if (transition.IsFailure)
            {
                row.Failed($"Could not set project '{key}' to {data.Status}: {transition.Error}");
                continue;
            }

            // Taken within the file as well as against the database: rows are applied before anything is
            // saved, so a repeat would otherwise only surface at the unique index.
            takenKeys.Add(key);
            row.Created(project.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly created (Proposed) project to its target status through the real transitions, so
    /// every guard the domain enforces is honoured — approval requires a lifecycle, activation a date
    /// range — and stamps each one with the date the row says it happened on.
    /// </summary>
    /// <remarks>
    /// The route is chosen by the dates rather than by the status alone. Which of them a row carries is
    /// settled by <c>ImportProjectDtoValidator</c> before any of this runs, so a missing one here is a
    /// validation gap rather than a case to fall back on.
    /// <para>
    /// Approval is stamped with <c>CreatedOn</c>: an approved project is still one that has not started,
    /// and the row does not separate the two. That leaves two history rows sharing an instant, which
    /// <c>ProjectStatusHistory.Sequence</c> already exists to order.
    /// </para>
    /// <para>
    /// Runs as <see cref="PpmActor.System"/>: import is a bulk administrative operation authorized by the
    /// caller's Permissions.Projects.Import claim, not by delivery-leadership membership. Membership
    /// gating cannot apply here in any case — the project is created by this same run, so nobody holds a
    /// role on it yet.
    /// </para>
    /// </remarks>
    private static Result ApplyStatus(Project project, ImportProjectDto data)
    {
        var actor = PpmActor.System;
        var ancestry = ProjectAncestryRoles.None;

        switch (data.Status)
        {
            case ProjectStatus.Proposed:
                return Result.Success();

            case ProjectStatus.Approved:
                return project.Approve(actor, ancestry, At(data.CreatedOn));

            case ProjectStatus.Active:
                return project.Activate(actor, ancestry, At(data.ActivatedOn!.Value));

            case ProjectStatus.Completed:
                var activate = project.Activate(actor, ancestry, At(data.ActivatedOn!.Value));
                return activate.IsFailure
                    ? activate
                    : project.Complete(actor, ancestry, At(data.ClosedOn!.Value));

            case ProjectStatus.Canceled:
                // Activation is optional here and nowhere else: a project can be canceled before it ever
                // started, and the status alone does not say whether this one was.
                if (data.ActivatedOn is { } activatedOn)
                {
                    var reached = project.Activate(actor, ancestry, At(activatedOn));
                    if (reached.IsFailure)
                        return reached;
                }

                return project.Cancel(actor, ancestry, At(data.ClosedOn!.Value));

            default:
                return Result.Failure($"Unsupported project status '{data.Status}'.");
        }
    }

    /// <summary>
    /// The instant a transition dated <paramref name="date"/> is recorded at. Status history is kept to
    /// the instant, and a row carries only a date, so the day is anchored at its UTC start.
    /// </summary>
    private static Instant At(LocalDate date) => date.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

    private async Task<HashSet<string>> ResolveTakenKeys(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        // Key is persisted through a value converter, so compare against ProjectKey instances — p.Key.Value
        // does not translate to SQL.
        var keys = context.Rows.Select(r => r.Data.Key).ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .AsNoTracking()
                .Where(p => keys.Contains(p.Key))
                .Select(p => p.Key)
                .ToListAsync(cancellationToken))
            .Select(k => k.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads each referenced portfolio with the programs and projects the aggregate needs in order to
    /// accept new ones and to validate the program each project names.
    /// </summary>
    private async Task<Dictionary<Guid, ProjectPortfolio>> ResolvePortfolios(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var portfolioIds = context.Rows.Select(r => r.Data.PortfolioId).Distinct().ToList();

        return await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.Programs)
            .Include(p => p.Projects)
            .Where(p => portfolioIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);
    }

    private async Task<HashSet<int>> ResolveExpenditureCategoryIds(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var categoryIds = context.Rows.Select(r => r.Data.ExpenditureCategoryId).Distinct().ToList();

        return (await _projectPortfolioManagementDbContext.ExpenditureCategories
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    /// <summary>
    /// Loads referenced lifecycles with their stages. Assigning a lifecycle copies those definitions onto
    /// the project, and the copies are what the project stage and task imports land against.
    /// </summary>
    private async Task<Dictionary<Guid, ProjectLifecycle>> ResolveLifecycles(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var lifecycleIds = context.Rows
            .Select(r => r.Data.ProjectLifecycleId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (lifecycleIds.Count == 0)
            return [];

        return await _projectPortfolioManagementDbContext.ProjectLifecycles
            .Include(l => l.Stages)
            .Where(l => lifecycleIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l, cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveActiveThemeIds(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var themeIds = context.Rows.SelectMany(r => r.Data.StrategicThemeIds).Distinct().ToList();

        if (themeIds.Count == 0)
            return [];

        return (await _projectPortfolioManagementDbContext.PpmStrategicThemes
                .AsNoTracking()
                .Where(t => themeIds.Contains(t.Id) && t.State == StrategicThemeState.Active)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(
        ImportPassContext<ImportProjectDto> context, CancellationToken cancellationToken)
    {
        var employeeNumbers = context.Rows
            .SelectMany(r => RoleEmployeeNumbers(r.Data))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (employeeNumbers.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        return (await _projectPortfolioManagementDbContext.Employees
                .AsNoTracking()
                .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
                .Select(e => new { e.Id, e.EmployeeNumber })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the highest existing project rank per portfolio, so imported projects continue the existing
    /// ranking instead of restarting it.
    /// </summary>
    private async Task<Dictionary<Guid, double?>> CurrentMaxRanks(
        IEnumerable<Guid> portfolioIds, CancellationToken cancellationToken)
    {
        var ids = portfolioIds.ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .AsNoTracking()
                .Where(p => ids.Contains(p.PortfolioId))
                .GroupBy(p => p.PortfolioId)
                .Select(g => new { PortfolioId = g.Key, MaxRank = g.Max(p => (double?)p.Rank) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.PortfolioId, x => x.MaxRank);
    }

    private static Dictionary<ProjectRole, HashSet<Guid>> BuildRoles(
        ImportProjectDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProjectRole, HashSet<Guid>> roles = [];

        Add(ProjectRole.Sponsor, data.SponsorEmployeeNumbers);
        Add(ProjectRole.Owner, data.OwnerEmployeeNumbers);
        Add(ProjectRole.Manager, data.ManagerEmployeeNumbers);
        Add(ProjectRole.Member, data.MemberEmployeeNumbers);

        return roles;

        void Add(ProjectRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProjectDto data) =>
        data.SponsorEmployeeNumbers
            .Concat(data.OwnerEmployeeNumbers)
            .Concat(data.ManagerEmployeeNumbers)
            .Concat(data.MemberEmployeeNumbers);

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
