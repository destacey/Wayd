using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;

/// <summary>
/// Imports portfolios, each walked toward its target status by replaying the real lifecycle transitions so
/// an imported portfolio is indistinguishable from one driven through the UI.
/// </summary>
/// <remarks>
/// Atomic, matching the single save the command it replaces did. Portfolios are what programs, projects
/// and initiatives are imported into, so a half-applied file leaves the rest of a PPM import resolving
/// some parents and rejecting others.
/// </remarks>
public sealed class ProjectPortfolioImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProjectPortfolioDto>(serializer)
{
    public const string ImportKey = "ppm.portfolios";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public override string Key => ImportKey;
    public override string DisplayName => "Portfolios";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.ProjectPortfolios;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportProjectPortfolioDto>> Steps =>
    [
        new("CreatePortfolios", ImportPassScope.WholeSet, CreatePortfolios),
    ];

    /// <summary>
    /// Rejects a row whose name is taken or whose people cannot be resolved, then creates what is left.
    /// </summary>
    private async Task<Result> CreatePortfolios(ImportPassContext<ImportProjectPortfolioDto> context, CancellationToken cancellationToken)
    {
        var takenNames = await ResolveTakenNames(context, cancellationToken);
        var employeeIdsByNumber = await ResolveEmployees(context, cancellationToken);

        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person created every row by
        // hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var name = Normalize(data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"A portfolio already exists named '{name}'.");
                continue;
            }

            var unresolved = RoleEmployeeNumbers(data)
                .Select(Normalize)
                .Where(n => !employeeIdsByNumber.ContainsKey(n))
                .Distinct()
                .ToList();
            if (unresolved.Count > 0)
            {
                row.Failed($"No employee was found with number {Quote(unresolved)}.");
                continue;
            }

            var portfolio = ProjectPortfolio.Create(
                name, data.Description.Trim(), BuildRoles(data, employeeIdsByNumber), actor, timestamp);

            var transition = ApplyStatus(portfolio, data, timestamp);
            if (transition.IsFailure)
            {
                row.Failed($"Could not set portfolio '{name}' to {data.Status}: {transition.Error}");
                continue;
            }

            await _projectPortfolioManagementDbContext.Portfolios.AddAsync(portfolio, cancellationToken);

            // Taken within the file as well as against the database: rows are applied before anything is
            // saved, so a repeat would otherwise only surface at the unique index.
            takenNames.Add(name);
            row.Created(portfolio.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly created (Proposed) portfolio to the furthest status it can hold before its contents
    /// exist, activating it with the row's own start date so the historical timeline is preserved.
    /// </summary>
    /// <remarks>
    /// The transitions are invoked on the aggregate directly rather than through the lifecycle commands
    /// because <c>Activate</c>/<c>Close</c> take the date from <c>IDateTimeProvider.Today</c> — going
    /// through them would stamp every imported portfolio with today's date.
    /// <para>
    /// A portfolio only accepts programs and projects while active but can only be closed once all of them
    /// are closed, so one destined to finish is imported active and closed afterwards by the finalize
    /// import, once its contents have landed.
    /// </para>
    /// <para>
    /// Runs as <see cref="PpmActor.System"/>: import is authorized by the caller's
    /// Permissions.ProjectPortfolios.Import claim, not by delivery-leadership membership — and the
    /// portfolio is created by this same run, so nobody holds a role on it yet.
    /// </para>
    /// </remarks>
    private static Result ApplyStatus(ProjectPortfolio portfolio, ImportProjectPortfolioDto row, Instant timestamp)
    {
        if (row.Status is ProjectPortfolioStatus.Proposed)
            return Result.Success();

        // Guaranteed present by ImportProjectPortfolioDtoValidator for any status past Proposed.
        var activate = portfolio.Activate(PpmActor.System, row.ActivatedOn!.Value, timestamp);
        if (activate.IsFailure)
            return activate;

        return row.Status is ProjectPortfolioStatus.OnHold
            ? portfolio.Pause(PpmActor.System, timestamp)
            : Result.Success();
    }

    private async Task<HashSet<string>> ResolveTakenNames(
        ImportPassContext<ImportProjectPortfolioDto> context, CancellationToken cancellationToken)
    {
        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        return (await _projectPortfolioManagementDbContext.Portfolios
                .AsNoTracking()
                .Where(p => names.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(
        ImportPassContext<ImportProjectPortfolioDto> context, CancellationToken cancellationToken)
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

    private static Dictionary<ProjectPortfolioRole, HashSet<Guid>> BuildRoles(
        ImportProjectPortfolioDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProjectPortfolioRole, HashSet<Guid>> roles = [];

        Add(ProjectPortfolioRole.Sponsor, data.SponsorEmployeeNumbers);
        Add(ProjectPortfolioRole.Owner, data.OwnerEmployeeNumbers);
        Add(ProjectPortfolioRole.Manager, data.ManagerEmployeeNumbers);

        return roles;

        void Add(ProjectPortfolioRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProjectPortfolioDto data) =>
        data.SponsorEmployeeNumbers.Concat(data.OwnerEmployeeNumbers).Concat(data.ManagerEmployeeNumbers);

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
