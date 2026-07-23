using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

/// <summary>
/// Additively imports a batch of projects, each created through its owning portfolio (the only path the
/// domain allows), optionally assigned a lifecycle, and then walked to its target status by replaying the
/// real lifecycle transitions.
/// <para>
/// Unlike programs and portfolios, a project has nothing beneath it that must close first, so it reaches
/// its true final status here — which is what lets the finalize import then close the programs and
/// portfolios containing it.
/// </para>
/// <para>
/// The batch is all-or-nothing: every reference is resolved by natural key up front and any name that is
/// duplicated, unresolved or ambiguous fails the whole import, so a mistyped reference can never quietly
/// attach a project to the wrong portfolio.
/// </para>
/// </summary>
public sealed record ImportProjectsCommand : ICommand
{
    public ImportProjectsCommand(IEnumerable<ImportProjectDto> projects)
    {
        Projects = [.. projects];
    }

    public List<ImportProjectDto> Projects { get; }
}

public sealed class ImportProjectsCommandValidator : CustomValidator<ImportProjectsCommand>
{
    public ImportProjectsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Projects)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Projects)
            .NotNull()
            .SetValidator(new ImportProjectDtoValidator());
    }
}

public sealed class ImportProjectsCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportProjectsCommandHandler> logger) : ICommandHandler<ImportProjectsCommand>
{
    private const string RequestName = nameof(ImportProjectsCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportProjectsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        try
        {
            var keysResult = await ValidateKeys(request, cancellationToken);
            if (keysResult.IsFailure)
                return keysResult;

            var portfoliosResult = await ResolvePortfolios(request, cancellationToken);
            if (portfoliosResult.IsFailure)
                return Result.Failure(portfoliosResult.Error);
            var portfoliosByName = portfoliosResult.Value;

            var categoriesResult = await ResolveExpenditureCategories(request, cancellationToken);
            if (categoriesResult.IsFailure)
                return Result.Failure(categoriesResult.Error);
            var categoryIdsByName = categoriesResult.Value;

            var lifecyclesResult = await ResolveLifecycles(request, cancellationToken);
            if (lifecyclesResult.IsFailure)
                return Result.Failure(lifecyclesResult.Error);
            var lifecyclesByName = lifecyclesResult.Value;

            var themesResult = await ResolveThemes(request, cancellationToken);
            if (themesResult.IsFailure)
                return Result.Failure(themesResult.Error);
            var themeIdsByName = themesResult.Value;

            var employeeNumbers = request.Projects
                .SelectMany(RoleEmployeeNumbers)
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var employeeIdsByNumber = await ResolveEmployees(employeeNumbers, cancellationToken);

            var unresolvedEmployees = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).ToList();
            if (unresolvedEmployees.Count > 0)
                return Fail($"Could not resolve the following employee numbers: {Quote(unresolvedEmployees)}.");

            // Projects are ranked at the bottom of their portfolio on creation, so the running max rank per
            // portfolio has to advance as the batch is applied — otherwise every imported project would be
            // handed the same rank.
            var maxRankByPortfolio = await CurrentMaxRanks(portfoliosByName.Values, cancellationToken);

            foreach (var row in request.Projects)
            {
                var portfolio = portfoliosByName[Normalize(row.PortfolioName)];

                var programId = ResolveProgramId(portfolio, row);
                if (programId.IsFailure)
                    return Fail(programId.Error);

                var dateRange = row.Start is null || row.End is null ? null : new LocalDateRange(row.Start.Value, row.End.Value);
                var themeIds = row.StrategicThemeNames.Select(n => themeIdsByName[Normalize(n)]).ToHashSet();
                maxRankByPortfolio.TryGetValue(portfolio.Id, out var currentMaxRank);

                var createResult = portfolio.CreateProject(
                    Normalize(row.Name),
                    row.Description.Trim(),
                    row.Key,
                    categoryIdsByName[Normalize(row.ExpenditureCategoryName)],
                    dateRange,
                    programId.Value,
                    row.BusinessCase?.Trim(),
                    row.ExpectedBenefits?.Trim(),
                    BuildRoles(row, employeeIdsByNumber),
                    themeIds,
                    timestamp,
                    currentMaxRank);
                if (createResult.IsFailure)
                    return Fail($"Could not create project '{row.Key.Value}' in portfolio '{row.PortfolioName}': {createResult.Error}");

                var project = createResult.Value;
                maxRankByPortfolio[portfolio.Id] = project.Rank;

                if (row.ProjectLifecycleName is not null)
                {
                    var assignResult = project.AssignLifecycle(lifecyclesByName[Normalize(row.ProjectLifecycleName)]);
                    if (assignResult.IsFailure)
                        return Fail($"Could not assign lifecycle '{row.ProjectLifecycleName}' to project '{row.Key.Value}': {assignResult.Error}");
                }

                var transition = ApplyStatus(project, row.Status);
                if (transition.IsFailure)
                    return Fail($"Could not set project '{row.Key.Value}' to {row.Status}: {transition.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} project(s).", RequestName, request.Projects.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks a freshly created (Proposed) project to its target status through the real transitions, so
    /// every guard the domain enforces is honoured — approval requires a lifecycle, activation a date
    /// range. A cancelled project is cancelled straight from Proposed, which the domain permits.
    /// </summary>
    private static Result ApplyStatus(Project project, ProjectStatus status)
    {
        switch (status)
        {
            case ProjectStatus.Proposed:
                return Result.Success();

            case ProjectStatus.Cancelled:
                return project.Cancel();

            case ProjectStatus.Approved:
                return project.Approve();

            case ProjectStatus.Active:
                return project.Activate();

            case ProjectStatus.Completed:
                var activate = project.Activate();
                return activate.IsFailure ? activate : project.Complete();

            default:
                return Result.Failure($"Unsupported project status '{status}'.");
        }
    }

    /// <summary>
    /// Rejects keys duplicated within the batch or already taken. Project keys are unique in the database,
    /// so catching this up front turns a mid-batch constraint violation into a clear message.
    /// </summary>
    private async Task<Result> ValidateKeys(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var duplicateKeys = request.Projects
            .GroupBy(p => p.Key.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateKeys.Count > 0)
            return Fail($"The following project keys appear more than once in the import: {Quote(duplicateKeys)}.");

        // Key is persisted through a value converter, so compare against ProjectKey instances — p.Key.Value
        // does not translate to SQL.
        var keys = request.Projects.Select(p => p.Key).ToList();

        var existingKeys = (await _projectPortfolioManagementDbContext.Projects
                .Where(p => keys.Contains(p.Key))
                .Select(p => p.Key)
                .ToListAsync(cancellationToken))
            .Select(k => k.Value)
            .ToList();
        if (existingKeys.Count > 0)
            return Fail($"The following project keys already exist: {Quote(existingKeys)}.");

        return Result.Success();
    }

    /// <summary>
    /// Resolves the row's program within its portfolio. Programs are scoped to their portfolio, so the name
    /// only has to be unambiguous there — and a program that has stopped accepting projects is reported by
    /// the aggregate itself when the project is created.
    /// </summary>
    private static Result<Guid?> ResolveProgramId(ProjectPortfolio portfolio, ImportProjectDto row)
    {
        if (row.ProgramName is null)
            return Result.Success<Guid?>(null);

        var matches = portfolio.Programs
            .Where(p => string.Equals(p.Name, Normalize(row.ProgramName), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => Result.Success<Guid?>(matches[0].Id),
            0 => Result.Failure<Guid?>($"Could not resolve program '{row.ProgramName}' in portfolio '{row.PortfolioName}' for project '{row.Key.Value}'."),
            _ => Result.Failure<Guid?>($"Program name '{row.ProgramName}' matches more than one program in portfolio '{row.PortfolioName}'."),
        };
    }

    /// <summary>
    /// Loads every referenced portfolio with the programs and projects the aggregate needs in order to
    /// accept new ones and to validate the program each project names.
    /// </summary>
    private async Task<Result<Dictionary<string, ProjectPortfolio>>> ResolvePortfolios(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var portfolioNames = request.Projects
            .Select(p => Normalize(p.PortfolioName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var portfolios = await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.Programs)
            .Include(p => p.Projects)
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

    private async Task<Result<Dictionary<string, int>>> ResolveExpenditureCategories(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var categoryNames = request.Projects
            .Select(p => Normalize(p.ExpenditureCategoryName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categories = await _projectPortfolioManagementDbContext.ExpenditureCategories
            .Where(c => categoryNames.Contains(c.Name))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var ambiguous = categories
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
            return Fail<Dictionary<string, int>>($"The following expenditure category names match more than one category: {Quote(ambiguous)}.");

        var categoryIdsByName = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var unresolved = categoryNames.Where(n => !categoryIdsByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, int>>($"Could not resolve the following expenditure categories: {Quote(unresolved)}.");

        return Result.Success(categoryIdsByName);
    }

    /// <summary>
    /// Loads referenced lifecycles with their phases, which the project copies when the lifecycle is
    /// assigned — those copied phases are what project tasks are later imported into.
    /// </summary>
    private async Task<Result<Dictionary<string, ProjectLifecycle>>> ResolveLifecycles(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var lifecycleNames = request.Projects
            .Select(p => p.ProjectLifecycleName)
            .Where(n => n is not null)
            .Select(n => Normalize(n!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (lifecycleNames.Count == 0)
            return Result.Success(new Dictionary<string, ProjectLifecycle>(StringComparer.OrdinalIgnoreCase));

        var lifecycles = await _projectPortfolioManagementDbContext.ProjectLifecycles
            .Include(l => l.Phases)
            .Where(l => lifecycleNames.Contains(l.Name))
            .ToListAsync(cancellationToken);

        var ambiguous = lifecycles
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
            return Fail<Dictionary<string, ProjectLifecycle>>($"The following project lifecycle names match more than one lifecycle: {Quote(ambiguous)}.");

        var lifecyclesByName = lifecycles.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

        var unresolved = lifecycleNames.Where(n => !lifecyclesByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, ProjectLifecycle>>($"Could not resolve the following project lifecycles: {Quote(unresolved)}.");

        return Result.Success(lifecyclesByName);
    }

    private async Task<Result<Dictionary<string, Guid>>> ResolveThemes(ImportProjectsCommand request, CancellationToken cancellationToken)
    {
        var themeNames = request.Projects
            .SelectMany(p => p.StrategicThemeNames)
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (themeNames.Count == 0)
            return Result.Success(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        var themes = await _projectPortfolioManagementDbContext.PpmStrategicThemes
            .Where(t => themeNames.Contains(t.Name))
            .Select(t => new { t.Id, t.Name, t.State })
            .ToListAsync(cancellationToken);

        var ambiguous = themes
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
            return Fail<Dictionary<string, Guid>>($"The following strategic theme names match more than one theme: {Quote(ambiguous)}.");

        var inactive = themes.Where(t => t.State != StrategicThemeState.Active).Select(t => t.Name).ToList();
        if (inactive.Count > 0)
            return Fail<Dictionary<string, Guid>>($"The following strategic themes are not active: {Quote(inactive)}.");

        var themeIdsByName = themes.ToDictionary(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase);

        var unresolved = themeNames.Where(n => !themeIdsByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, Guid>>($"Could not resolve the following strategic themes: {Quote(unresolved)}.");

        return Result.Success(themeIdsByName);
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

    /// <summary>
    /// Reads the highest existing project rank per portfolio, so imported projects continue the existing
    /// ranking instead of restarting it.
    /// </summary>
    private async Task<Dictionary<Guid, double?>> CurrentMaxRanks(IEnumerable<ProjectPortfolio> portfolios, CancellationToken cancellationToken)
    {
        var portfolioIds = portfolios.Select(p => p.Id).ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .Where(p => portfolioIds.Contains(p.PortfolioId))
                .GroupBy(p => p.PortfolioId)
                .Select(g => new { PortfolioId = g.Key, MaxRank = g.Max(p => (double?)p.Rank) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.PortfolioId, x => x.MaxRank);
    }

    private static Dictionary<ProjectRole, HashSet<Guid>> BuildRoles(ImportProjectDto row, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProjectRole, HashSet<Guid>> roles = [];

        Add(ProjectRole.Sponsor, row.SponsorEmployeeNumbers);
        Add(ProjectRole.Owner, row.OwnerEmployeeNumbers);
        Add(ProjectRole.Manager, row.ManagerEmployeeNumbers);
        Add(ProjectRole.Member, row.MemberEmployeeNumbers);

        return roles;

        void Add(ProjectRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProjectDto row) =>
        row.SponsorEmployeeNumbers
            .Concat(row.OwnerEmployeeNumbers)
            .Concat(row.ManagerEmployeeNumbers)
            .Concat(row.MemberEmployeeNumbers);

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
