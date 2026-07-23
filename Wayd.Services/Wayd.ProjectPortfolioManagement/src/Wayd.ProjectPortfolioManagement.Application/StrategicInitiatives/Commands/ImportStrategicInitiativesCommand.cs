using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

/// <summary>
/// Additively imports a batch of strategic initiatives with their KPIs, each created through its owning
/// portfolio and then walked to its target status by replaying the real lifecycle transitions.
/// <para>
/// Projects and KPIs are attached before the status is applied, because a closed initiative refuses both.
/// Unlike programs and portfolios, an initiative has nothing beneath it that must close first, so it
/// reaches its true final status here and needs no finalization pass.
/// </para>
/// </summary>
public sealed record ImportStrategicInitiativesCommand : ICommand
{
    public ImportStrategicInitiativesCommand(
        IEnumerable<ImportStrategicInitiativeDto> strategicInitiatives,
        IEnumerable<ImportStrategicInitiativeKpiDto>? kpis = null)
    {
        StrategicInitiatives = [.. strategicInitiatives];
        Kpis = [.. kpis ?? []];
    }

    public List<ImportStrategicInitiativeDto> StrategicInitiatives { get; }

    public List<ImportStrategicInitiativeKpiDto> Kpis { get; }
}

public sealed class ImportStrategicInitiativesCommandValidator : CustomValidator<ImportStrategicInitiativesCommand>
{
    public ImportStrategicInitiativesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.StrategicInitiatives)
            .NotNull()
            .NotEmpty();

        RuleForEach(i => i.StrategicInitiatives)
            .NotNull()
            .SetValidator(new ImportStrategicInitiativeDtoValidator());

        RuleForEach(i => i.Kpis)
            .NotNull()
            .SetValidator(new ImportStrategicInitiativeKpiDtoValidator());
    }
}

public sealed class ImportStrategicInitiativesCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportStrategicInitiativesCommandHandler> logger) : ICommandHandler<ImportStrategicInitiativesCommand>
{
    private const string RequestName = nameof(ImportStrategicInitiativesCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportStrategicInitiativesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportStrategicInitiativesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var duplicates = request.StrategicInitiatives
                .GroupBy(i => Normalize(i.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following strategic initiative names appear more than once in the import: {Quote(duplicates)}.");

            var names = request.StrategicInitiatives.Select(i => Normalize(i.Name)).ToList();

            var existingNames = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Where(i => names.Contains(i.Name))
                .Select(i => i.Name)
                .ToListAsync(cancellationToken);
            if (existingNames.Count > 0)
                return Fail($"The following strategic initiatives already exist: {Quote(existingNames)}.");

            var orphanKpis = request.Kpis
                .Select(k => Normalize(k.StrategicInitiativeName))
                .Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (orphanKpis.Count > 0)
                return Fail($"The following KPI rows name a strategic initiative that is not in the import: {Quote(orphanKpis)}.");

            var portfoliosResult = await ResolvePortfolios(request, cancellationToken);
            if (portfoliosResult.IsFailure)
                return Result.Failure(portfoliosResult.Error);
            var portfoliosByName = portfoliosResult.Value;

            var projectsResult = await ResolveProjects(request, cancellationToken);
            if (projectsResult.IsFailure)
                return Result.Failure(projectsResult.Error);
            var projectIdsByKey = projectsResult.Value;

            var employeeNumbers = request.StrategicInitiatives
                .SelectMany(i => i.SponsorEmployeeNumbers.Concat(i.OwnerEmployeeNumbers))
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var employeeIdsByNumber = await ResolveEmployees(employeeNumbers, cancellationToken);

            var unresolvedEmployees = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).ToList();
            if (unresolvedEmployees.Count > 0)
                return Fail($"Could not resolve the following employee numbers: {Quote(unresolvedEmployees)}.");

            var kpisByInitiative = request.Kpis
                .GroupBy(k => Normalize(k.StrategicInitiativeName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in request.StrategicInitiatives)
            {
                var portfolio = portfoliosByName[Normalize(row.PortfolioName)];

                var createResult = portfolio.CreateStrategicInitiative(
                    Normalize(row.Name),
                    row.Description.Trim(),
                    new LocalDateRange(row.Start, row.End),
                    BuildRoles(row, employeeIdsByNumber));
                if (createResult.IsFailure)
                    return Fail($"Could not create strategic initiative '{row.Name}' in portfolio '{row.PortfolioName}': {createResult.Error}");

                var initiative = createResult.Value;

                // Both of these are refused once the initiative is closed, so they precede the status walk.
                if (row.ProjectKeys.Count > 0)
                {
                    var manageResult = initiative.ManageProjects(row.ProjectKeys.Select(k => projectIdsByKey[Normalize(k)]));
                    if (manageResult.IsFailure)
                        return Fail($"Could not attach projects to strategic initiative '{row.Name}': {manageResult.Error}");
                }

                if (kpisByInitiative.TryGetValue(Normalize(row.Name), out var kpiRows))
                {
                    var kpiResult = AddKpis(initiative, kpiRows);
                    if (kpiResult.IsFailure)
                        return kpiResult;
                }

                var transition = ApplyStatus(initiative, row.Status);
                if (transition.IsFailure)
                    return Fail($"Could not set strategic initiative '{row.Name}' to {row.Status}: {transition.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "{RequestName}: imported {InitiativeCount} strategic initiative(s) and {KpiCount} KPI(s).",
                RequestName, request.StrategicInitiatives.Count, request.Kpis.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks a freshly created (Proposed) initiative to its target status through the real transitions.
    /// Activation only follows approval, so reaching Active or beyond replays the whole chain.
    /// </summary>
    private static Result ApplyStatus(StrategicInitiative initiative, StrategicInitiativeStatus status)
    {
        if (status is StrategicInitiativeStatus.Proposed)
            return Result.Success();

        if (status is StrategicInitiativeStatus.Cancelled)
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

    private Result AddKpis(StrategicInitiative initiative, List<ImportStrategicInitiativeKpiDto> kpiRows)
    {
        var duplicates = kpiRows
            .GroupBy(k => Normalize(k.Name), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            return Fail($"The following KPI names appear more than once for strategic initiative '{initiative.Name}': {Quote(duplicates)}.");

        foreach (var kpi in kpiRows)
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
                return Fail($"Could not create KPI '{kpi.Name}' for strategic initiative '{initiative.Name}': {result.Error}");
        }

        return Result.Success();
    }

    private async Task<Result<Dictionary<string, ProjectPortfolio>>> ResolvePortfolios(ImportStrategicInitiativesCommand request, CancellationToken cancellationToken)
    {
        var portfolioNames = request.StrategicInitiatives
            .Select(i => Normalize(i.PortfolioName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var portfolios = await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.StrategicInitiatives)
            .Where(p => portfolioNames.Contains(p.Name))
            .ToListAsync(cancellationToken);

        var ambiguous = portfolios
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
            return Fail<Dictionary<string, ProjectPortfolio>>($"The following portfolio names match more than one portfolio: {Quote(ambiguous)}.");

        var portfoliosByName = portfolios.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var unresolved = portfolioNames.Where(n => !portfoliosByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, ProjectPortfolio>>($"Could not resolve the following portfolios: {Quote(unresolved)}.");

        return Result.Success(portfoliosByName);
    }

    private async Task<Result<Dictionary<string, Guid>>> ResolveProjects(ImportStrategicInitiativesCommand request, CancellationToken cancellationToken)
    {
        var projectKeys = request.StrategicInitiatives
            .SelectMany(i => i.ProjectKeys)
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (projectKeys.Count == 0)
            return Result.Success(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        // Key is persisted through a value converter, so compare against ProjectKey instances.
        var keys = projectKeys.Select(k => new ProjectKey(k)).ToList();

        var projects = (await _projectPortfolioManagementDbContext.Projects
                .Where(p => keys.Contains(p.Key))
                .Select(p => new { p.Id, p.Key })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Key.Value, p => p.Id, StringComparer.OrdinalIgnoreCase);

        var unresolved = projectKeys.Where(k => !projects.ContainsKey(k)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, Guid>>($"Could not resolve the following projects: {Quote(unresolved)}.");

        return Result.Success(projects);
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(HashSet<string> employeeNumbers, CancellationToken cancellationToken)
    {
        if (employeeNumbers.Count == 0)
            return [];

        return (await _projectPortfolioManagementDbContext.Employees
                .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
                .Select(e => new { e.Id, e.EmployeeNumber })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<StrategicInitiativeRole, HashSet<Guid>> BuildRoles(ImportStrategicInitiativeDto row, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<StrategicInitiativeRole, HashSet<Guid>> roles = [];

        Add(StrategicInitiativeRole.Sponsor, row.SponsorEmployeeNumbers);
        Add(StrategicInitiativeRole.Owner, row.OwnerEmployeeNumbers);

        return roles;

        void Add(StrategicInitiativeRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private Result<T> Fail<T>(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure<T>(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
