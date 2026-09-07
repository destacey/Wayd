using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;

/// <summary>
/// Imports strategic initiatives with the KPIs carried on each row.
/// </summary>
/// <remarks>
/// Atomic, matching the single save the command it replaces did.
/// <para>
/// One pass, because a row depends on nothing else in the file: its portfolio, projects and people all
/// have to exist already, and its KPIs travel with it.
/// </para>
/// </remarks>
public sealed class StrategicInitiativeImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportStrategicInitiativeDto>(serializer)
{
    public const string ImportKey = "ppm.strategic-initiatives";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Strategic Initiatives";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.StrategicInitiatives;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportStrategicInitiativeDto>> Steps =>
    [
        new("CreateInitiatives", ImportPassScope.WholeSet, CreateInitiatives),
    ];

    /// <summary>
    /// Resolves every reference and rejects what cannot be found, then creates what is left.
    /// </summary>
    private async Task<Result> CreateInitiatives(ImportPassContext<ImportStrategicInitiativeDto> context, CancellationToken cancellationToken)
    {
        var portfoliosById = await ResolvePortfolios(context, cancellationToken);
        var projectIdsByKey = await ResolveProjects(context, cancellationToken);
        var employeeIdsByNumber = await ResolveEmployees(context, cancellationToken);

        var takenNames = await ResolveTakenNames(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var name = Normalize(data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"A strategic initiative already exists named '{name}'.");
                continue;
            }

            if (!portfoliosById.TryGetValue(data.PortfolioId, out var portfolio))
            {
                row.Failed($"No portfolio was found with id '{data.PortfolioId}'.");
                continue;
            }

            var unresolvedProjects = data.ProjectKeys
                .Select(Normalize)
                .Where(k => !projectIdsByKey.ContainsKey(k))
                .ToList();
            if (unresolvedProjects.Count > 0)
            {
                row.Failed($"No project was found with key {Quote(unresolvedProjects)}.");
                continue;
            }

            var employeeNumbers = data.SponsorEmployeeNumbers.Concat(data.OwnerEmployeeNumbers).Select(Normalize);
            var unresolvedEmployees = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).Distinct().ToList();
            if (unresolvedEmployees.Count > 0)
            {
                row.Failed($"No employee was found with number {Quote(unresolvedEmployees)}.");
                continue;
            }

            var created = portfolio.CreateStrategicInitiative(
                name,
                data.Description.Trim(),
                new LocalDateRange(data.Start, data.End),
                BuildRoles(data, employeeIdsByNumber));
            if (created.IsFailure)
            {
                row.Failed($"Could not create strategic initiative '{name}': {created.Error}");
                continue;
            }

            var initiative = created.Value;

            // Projects and KPIs are both refused once the initiative is closed, so they precede the
            // status walk.
            if (data.ProjectKeys.Count > 0)
            {
                var managed = initiative.ManageProjects(data.ProjectKeys.Select(k => projectIdsByKey[Normalize(k)]));
                if (managed.IsFailure)
                {
                    row.Failed($"Could not attach projects to strategic initiative '{name}': {managed.Error}");
                    continue;
                }
            }

            var kpis = AddKpis(initiative, data.Kpis);
            if (kpis.IsFailure)
            {
                row.Failed(kpis.Error);
                continue;
            }

            var transition = ApplyStatus(initiative, data.Status);
            if (transition.IsFailure)
            {
                row.Failed($"Could not set strategic initiative '{name}' to {data.Status}: {transition.Error}");
                continue;
            }

            // Taken within the file as well as against the database: rows are applied one at a time
            // against a portfolio held in memory, so a repeat would otherwise reach the unique index.
            takenNames.Add(name);
            row.Created(initiative.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly created (Proposed) initiative to its target status through the real transitions.
    /// Activation only follows approval, so reaching Active or beyond replays the whole chain.
    /// </summary>
    private static Result ApplyStatus(StrategicInitiative initiative, StrategicInitiativeStatus status)
    {
        if (status is StrategicInitiativeStatus.Proposed)
            return Result.Success();

        if (status is StrategicInitiativeStatus.Canceled)
            return initiative.Cancel();

        var approve = initiative.Approve();
        if (approve.IsFailure || status is StrategicInitiativeStatus.Approved)
            return approve;

        var activate = initiative.Activate();
        if (activate.IsFailure || status is StrategicInitiativeStatus.Active)
            return activate;

        // The domain defines OnHold but exposes no transition that reaches it, so an import cannot honour
        // it without bypassing the aggregate. Reject the row rather than quietly importing a different
        // status than the one it asked for.
        if (status is StrategicInitiativeStatus.OnHold)
            return Result.Failure("Strategic initiatives cannot be imported on hold: the domain has no transition to that status.");

        return initiative.Complete();
    }

    private static Result AddKpis(StrategicInitiative initiative, IReadOnlyList<ImportStrategicInitiativeKpiDto> kpis)
    {
        foreach (var kpi in kpis)
        {
            var parameters = new StrategicInitiativeKpiUpsertParameters(
                Normalize(kpi.Name),
                kpi.Description?.Trim(),
                kpi.StartingValue,
                kpi.TargetValue,
                kpi.Prefix?.Trim(),
                kpi.Suffix?.Trim(),
                kpi.TargetDirection);

            var result = initiative.CreateKpi(parameters);
            if (result.IsFailure)
                return Result.Failure($"Could not create KPI '{kpi.Name}' for strategic initiative '{initiative.Name}': {result.Error}");
        }

        return Result.Success();
    }

    private async Task<Dictionary<Guid, ProjectPortfolio>> ResolvePortfolios(
        ImportPassContext<ImportStrategicInitiativeDto> context, CancellationToken cancellationToken)
    {
        var portfolioIds = context.Rows.Select(r => r.Data.PortfolioId).Distinct().ToList();

        // Tracked, and with the initiatives loaded: CreateStrategicInitiative appends to that collection,
        // and the portfolio is what carries the new row into the save.
        return await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.StrategicInitiatives)
            .Where(p => portfolioIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> ResolveProjects(
        ImportPassContext<ImportStrategicInitiativeDto> context, CancellationToken cancellationToken)
    {
        var projectKeys = context.Rows
            .SelectMany(r => r.Data.ProjectKeys)
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (projectKeys.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Key is persisted through a value converter, so compare against ProjectKey instances: the
        // property translates but a member of it does not.
        var keys = projectKeys.Select(k => new ProjectKey(k)).ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .AsNoTracking()
                .Where(p => keys.Contains(p.Key))
                .Select(p => new { p.Id, p.Key })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Key.Value, p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(
        ImportPassContext<ImportStrategicInitiativeDto> context, CancellationToken cancellationToken)
    {
        var employeeNumbers = context.Rows
            .SelectMany(r => r.Data.SponsorEmployeeNumbers.Concat(r.Data.OwnerEmployeeNumbers))
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

    private async Task<HashSet<string>> ResolveTakenNames(
        ImportPassContext<ImportStrategicInitiativeDto> context, CancellationToken cancellationToken)
    {
        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        return (await _projectPortfolioManagementDbContext.StrategicInitiatives
                .AsNoTracking()
                .Where(i => names.Contains(i.Name))
                .Select(i => i.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<StrategicInitiativeRole, HashSet<Guid>> BuildRoles(
        ImportStrategicInitiativeDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<StrategicInitiativeRole, HashSet<Guid>> roles = [];

        Add(StrategicInitiativeRole.Sponsor, data.SponsorEmployeeNumbers);
        Add(StrategicInitiativeRole.Owner, data.OwnerEmployeeNumbers);

        return roles;

        void Add(StrategicInitiativeRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
