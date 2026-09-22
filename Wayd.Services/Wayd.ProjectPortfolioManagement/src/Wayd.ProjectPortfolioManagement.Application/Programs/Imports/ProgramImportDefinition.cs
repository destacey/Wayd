using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Imports;

/// <summary>
/// Imports programs into the portfolios that own them.
/// </summary>
/// <remarks>
/// Per group, keyed on the program the row creates. A row is not finished when the program is created — it
/// is then walked to its status — so a rejection partway has already staged the program, and the group is
/// what makes the runner discard it rather than commit it beside the rows that succeeded.
/// <para>
/// Programs are independent of one another, so the group is the program rather than its portfolio. The
/// project import names its program by the id this run reported, so a rejected program is reported again by
/// every project that named it; that is a clearer failure than keeping every program out.
/// </para>
/// </remarks>
public sealed class ProgramImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProgramDto>(serializer)
{
    public const string ImportKey = "ppm.programs";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;

    public override string Key => ImportKey;
    public override string DisplayName => "Programs";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Programs;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "program";

    // Saving chunk by chunk makes a larger file possible, but a run at that size is not yet proven end to
    // end, so the cap stays where the all-or-nothing version had it. The preflight bound follows it, and
    // would have to anyway: a preflight is one transaction rolled back at the end, so it cannot release
    // its locks chunk by chunk the way a real run does — there, the file size is the lock time.
    public override int MaxRows => 10_000;

    // The name, trimmed the one way the duplicate check trims it. Not case-folded: two rows naming the same
    // program in different casing can never both apply — the second is refused as a duplicate whichever
    // chunk it lands in — so they have no need to share a group, and folding a 128-character name risks
    // passing the group key's own 128-character bound.
    protected override string? GroupKey(ImportProgramDto row) => Normalize(row.Name);

    protected override IReadOnlyList<ImportPass<ImportProgramDto>> Steps =>
    [
        new("CreatePrograms", ImportPassScope.Chunked, CreatePrograms),
    ];

    /// <summary>
    /// Resolves every reference and rejects what cannot be found, then creates what is left.
    /// </summary>
    private async Task<Result> CreatePrograms(ImportPassContext<ImportProgramDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person edited every row by
        // hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        var takenNames = await ResolveTakenNames(context, cancellationToken);
        var portfoliosById = await ResolvePortfolios(context, cancellationToken);
        var activeThemeIds = await ResolveActiveThemeIds(context, cancellationToken);
        var employeeIdsByNumber = await ResolveEmployees(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var name = Normalize(data.Name);

            if (takenNames.Contains(name))
            {
                row.Failed($"A program already exists named '{name}'.");
                continue;
            }

            if (!portfoliosById.TryGetValue(data.PortfolioId, out var portfolio))
            {
                row.Failed($"No portfolio was found with id '{data.PortfolioId}'.");
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

            var created = portfolio.CreateProgram(
                name,
                data.Description.Trim(),
                dateRange,
                BuildRoles(data, employeeIdsByNumber),
                [.. data.StrategicThemeIds],
                actor,
                timestamp);
            if (created.IsFailure)
            {
                row.Failed($"Could not create program '{name}' in portfolio '{portfolio.Name}': {created.Error}");
                continue;
            }

            var transition = ApplyStatus(created.Value, data.Status, timestamp);
            if (transition.IsFailure)
            {
                row.Failed($"Could not set program '{name}' to {data.Status}: {transition.Error}");
                continue;
            }

            // Taken within the chunk as well as against the database: a chunk's rows are applied before any
            // of them is saved, so a repeat would otherwise only surface at the unique index. A repeat in a
            // later chunk is caught by the query above, which sees what the earlier chunk saved.
            takenNames.Add(name);
            row.Created(created.Value.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// Walks a freshly created (Proposed) program to the furthest status it can hold before its projects
    /// exist.
    /// </summary>
    /// <remarks>
    /// A program only accepts projects while Active but can only be completed or canceled once all of them
    /// are closed, so one destined to finish is imported Active and closed afterwards by the finalize
    /// import, once the projects have landed.
    /// <para>
    /// Runs as <see cref="PpmActor.System"/>: import is authorized by the caller's
    /// Permissions.Programs.Import claim, not by delivery-leadership membership — and the program is
    /// created by this same run, so nobody holds a role on it yet.
    /// </para>
    /// </remarks>
    private static Result ApplyStatus(Program program, ProgramStatus status, Instant timestamp) =>
        status is ProgramStatus.Proposed
            ? Result.Success()
            : program.Activate(PpmActor.System, ProgramAncestryRoles.None, timestamp);

    private async Task<HashSet<string>> ResolveTakenNames(
        ImportPassContext<ImportProgramDto> context, CancellationToken cancellationToken)
    {
        var names = context.Rows.Select(r => Normalize(r.Data.Name)).ToList();

        return (await _projectPortfolioManagementDbContext.Programs
                .AsNoTracking()
                .Where(p => names.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Loads each referenced portfolio with its programs, so the aggregate can accept new ones.</summary>
    private async Task<Dictionary<Guid, ProjectPortfolio>> ResolvePortfolios(
        ImportPassContext<ImportProgramDto> context, CancellationToken cancellationToken)
    {
        var portfolioIds = context.Rows.Select(r => r.Data.PortfolioId).Distinct().ToList();

        return await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.Programs)
            .Where(p => portfolioIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveActiveThemeIds(
        ImportPassContext<ImportProgramDto> context, CancellationToken cancellationToken)
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
        ImportPassContext<ImportProgramDto> context, CancellationToken cancellationToken)
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

    private static Dictionary<ProgramRole, HashSet<Guid>> BuildRoles(
        ImportProgramDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProgramRole, HashSet<Guid>> roles = [];

        Add(ProgramRole.Sponsor, data.SponsorEmployeeNumbers);
        Add(ProgramRole.Owner, data.OwnerEmployeeNumbers);
        Add(ProgramRole.Manager, data.ManagerEmployeeNumbers);

        return roles;

        void Add(ProgramRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProgramDto data) =>
        data.SponsorEmployeeNumbers.Concat(data.OwnerEmployeeNumbers).Concat(data.ManagerEmployeeNumbers);

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
