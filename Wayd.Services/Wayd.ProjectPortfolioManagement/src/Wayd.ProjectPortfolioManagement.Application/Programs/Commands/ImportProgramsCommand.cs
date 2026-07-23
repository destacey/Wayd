using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

/// <summary>
/// Additively imports a batch of programs, each created through its owning portfolio (the only path the
/// domain allows) and then walked toward its target status by replaying the real lifecycle transitions.
/// <para>
/// Programs destined to be completed or cancelled are imported active and finished later by the finalize
/// import: a program only accepts projects while active, and can only be completed once all of them are
/// closed, so its final status cannot be set until its projects exist.
/// </para>
/// <para>
/// The batch is all-or-nothing: portfolios, strategic themes and people are resolved by natural key up
/// front and any name that is duplicated, unresolved or ambiguous fails the whole import, so a mistyped
/// reference can never quietly attach a program to the wrong portfolio.
/// </para>
/// </summary>
public sealed record ImportProgramsCommand : ICommand
{
    public ImportProgramsCommand(IEnumerable<ImportProgramDto> programs)
    {
        Programs = [.. programs];
    }

    public List<ImportProgramDto> Programs { get; }
}

public sealed class ImportProgramsCommandValidator : CustomValidator<ImportProgramsCommand>
{
    public ImportProgramsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Programs)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Programs)
            .NotNull()
            .SetValidator(new ImportProgramDtoValidator());
    }
}

public sealed class ImportProgramsCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportProgramsCommandHandler> logger) : ICommandHandler<ImportProgramsCommand>
{
    private const string RequestName = nameof(ImportProgramsCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportProgramsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProgramsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        try
        {
            var duplicates = request.Programs
                .GroupBy(p => Normalize(p.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following program names appear more than once in the import: {Quote(duplicates)}.");

            var names = request.Programs.Select(p => Normalize(p.Name)).ToList();

            var existingNames = await _projectPortfolioManagementDbContext.Programs
                .Where(p => names.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);
            if (existingNames.Count > 0)
                return Fail($"The following programs already exist: {Quote(existingNames)}.");

            var portfoliosResult = await ResolvePortfolios(request, cancellationToken);
            if (portfoliosResult.IsFailure)
                return Result.Failure(portfoliosResult.Error);
            var portfoliosByName = portfoliosResult.Value;

            var themesResult = await ResolveThemes(request, cancellationToken);
            if (themesResult.IsFailure)
                return Result.Failure(themesResult.Error);
            var themeIdsByName = themesResult.Value;

            var employeeNumbers = request.Programs
                .SelectMany(RoleEmployeeNumbers)
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var employeeIdsByNumber = await ResolveEmployees(employeeNumbers, cancellationToken);

            var unresolvedEmployees = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).ToList();
            if (unresolvedEmployees.Count > 0)
                return Fail($"Could not resolve the following employee numbers: {Quote(unresolvedEmployees)}.");

            foreach (var row in request.Programs)
            {
                var portfolio = portfoliosByName[Normalize(row.PortfolioName)];
                var dateRange = row.Start is null || row.End is null ? null : new LocalDateRange(row.Start.Value, row.End.Value);
                var themeIds = row.StrategicThemeNames.Select(n => themeIdsByName[Normalize(n)]).ToHashSet();

                var createResult = portfolio.CreateProgram(
                    Normalize(row.Name),
                    row.Description.Trim(),
                    dateRange,
                    BuildRoles(row, employeeIdsByNumber),
                    themeIds,
                    timestamp);
                if (createResult.IsFailure)
                    return Fail($"Could not create program '{row.Name}' in portfolio '{row.PortfolioName}': {createResult.Error}");

                var transition = ApplyStatus(createResult.Value, row.Status);
                if (transition.IsFailure)
                    return Fail($"Could not set program '{row.Name}' to {row.Status}: {transition.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} program(s).", RequestName, request.Programs.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks a freshly created (Proposed) program to the furthest status it can hold before its projects
    /// exist. A program only accepts projects while Active, but can only be completed or cancelled once all
    /// of them are closed — so a program destined to finish still has to be imported Active and closed
    /// afterwards by the finalize import, once the projects have landed.
    /// </summary>
    private static Result ApplyStatus(Program program, ProgramStatus status) =>
        status is ProgramStatus.Proposed
            ? Result.Success()
            : program.Activate();

    /// <summary>
    /// Loads every referenced portfolio with its programs so the aggregate can accept new ones. Portfolio
    /// names are not unique in the database, so an ambiguous name fails the batch rather than guessing.
    /// </summary>
    private async Task<Result<Dictionary<string, ProjectPortfolio>>> ResolvePortfolios(ImportProgramsCommand request, CancellationToken cancellationToken)
    {
        var portfolioNames = request.Programs
            .Select(p => Normalize(p.PortfolioName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var portfolios = await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.Programs)
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

    /// <summary>
    /// Resolves strategic themes by name from the PPM projection. Only active themes can be attached, so an
    /// inactive one is reported rather than silently dropped.
    /// </summary>
    private async Task<Result<Dictionary<string, Guid>>> ResolveThemes(ImportProgramsCommand request, CancellationToken cancellationToken)
    {
        var themeNames = request.Programs
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

    private static Dictionary<ProgramRole, HashSet<Guid>> BuildRoles(ImportProgramDto row, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProgramRole, HashSet<Guid>> roles = [];

        Add(ProgramRole.Sponsor, row.SponsorEmployeeNumbers);
        Add(ProgramRole.Owner, row.OwnerEmployeeNumbers);
        Add(ProgramRole.Manager, row.ManagerEmployeeNumbers);

        return roles;

        void Add(ProgramRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProgramDto row) =>
        row.SponsorEmployeeNumbers.Concat(row.OwnerEmployeeNumbers).Concat(row.ManagerEmployeeNumbers);

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
