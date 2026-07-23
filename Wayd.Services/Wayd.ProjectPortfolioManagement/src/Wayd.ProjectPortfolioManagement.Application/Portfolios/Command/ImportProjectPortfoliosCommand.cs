using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

/// <summary>
/// Additively imports a batch of portfolios, each created through the domain factory and then walked toward
/// its target status by replaying the real lifecycle transitions, so a seeded portfolio is
/// indistinguishable from one driven through the UI.
/// <para>
/// The transitions are invoked on the aggregate directly rather than through the individual lifecycle
/// commands because <c>Activate</c>/<c>Close</c> take the date from <c>IDateTimeProvider.Today</c> — going
/// through them would stamp every imported portfolio with today's date and flatten the historical timeline.
/// </para>
/// <para>
/// Portfolios destined to be closed or archived are imported active and finished later by the finalize
/// import: a portfolio has to be active to receive programs and projects, and can only be closed once all
/// of them are closed, so its final status cannot be set until its contents exist.
/// </para>
/// Portfolios are the natural-key anchor for programs, projects and initiatives, so the batch is
/// all-or-nothing and rejects any name duplicated within the batch or already present, along with any
/// employee number that cannot be resolved.
/// </summary>
public sealed record ImportProjectPortfoliosCommand : ICommand
{
    public ImportProjectPortfoliosCommand(IEnumerable<ImportProjectPortfolioDto> portfolios)
    {
        Portfolios = [.. portfolios];
    }

    public List<ImportProjectPortfolioDto> Portfolios { get; }
}

public sealed class ImportProjectPortfoliosCommandValidator : CustomValidator<ImportProjectPortfoliosCommand>
{
    public ImportProjectPortfoliosCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Portfolios)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Portfolios)
            .NotNull()
            .SetValidator(new ImportProjectPortfolioDtoValidator());
    }
}

public sealed class ImportProjectPortfoliosCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportProjectPortfoliosCommandHandler> logger) : ICommandHandler<ImportProjectPortfoliosCommand>
{
    private const string RequestName = nameof(ImportProjectPortfoliosCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportProjectPortfoliosCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProjectPortfoliosCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var duplicates = request.Portfolios
                .GroupBy(p => Normalize(p.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following portfolio names appear more than once in the import: {Quote(duplicates)}.");

            var names = request.Portfolios.Select(p => Normalize(p.Name)).ToList();

            var existing = await _projectPortfolioManagementDbContext.Portfolios
                .Where(p => names.Contains(p.Name))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
                return Fail($"The following portfolios already exist: {Quote(existing)}.");

            var employeeNumbers = request.Portfolios
                .SelectMany(RoleEmployeeNumbers)
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var employeeIdsByNumber = await ResolveEmployees(employeeNumbers, cancellationToken);

            var unresolved = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).ToList();
            if (unresolved.Count > 0)
                return Fail($"Could not resolve the following employee numbers: {Quote(unresolved)}.");

            foreach (var row in request.Portfolios)
            {
                var portfolio = ProjectPortfolio.Create(
                    Normalize(row.Name),
                    row.Description.Trim(),
                    BuildRoles(row, employeeIdsByNumber));

                await _projectPortfolioManagementDbContext.Portfolios.AddAsync(portfolio, cancellationToken);

                var transition = ApplyStatus(portfolio, row);
                if (transition.IsFailure)
                    return Fail($"Could not set portfolio '{row.Name}' to {row.Status}: {transition.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} portfolio(s).", RequestName, request.Portfolios.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks a freshly created (Proposed) portfolio to the furthest status it can hold before its contents
    /// exist, activating it with the row's own start date so the historical timeline is preserved.
    /// A portfolio only accepts programs and projects while active, but can only be closed once all of them
    /// are closed — so a portfolio destined to finish is imported active here and closed afterwards by the
    /// finalize import, once its contents have landed.
    /// </summary>
    private static Result ApplyStatus(ProjectPortfolio portfolio, ImportProjectPortfolioDto row)
    {
        if (row.Status is ProjectPortfolioStatus.Proposed)
            return Result.Success();

        var activate = portfolio.Activate(row.Start!.Value);
        if (activate.IsFailure)
            return activate;

        return row.Status is ProjectPortfolioStatus.OnHold
            ? portfolio.Pause()
            : Result.Success();
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

    private static Dictionary<ProjectPortfolioRole, HashSet<Guid>> BuildRoles(ImportProjectPortfolioDto row, Dictionary<string, Guid> employeeIdsByNumber)
    {
        Dictionary<ProjectPortfolioRole, HashSet<Guid>> roles = [];

        Add(ProjectPortfolioRole.Sponsor, row.SponsorEmployeeNumbers);
        Add(ProjectPortfolioRole.Owner, row.OwnerEmployeeNumbers);
        Add(ProjectPortfolioRole.Manager, row.ManagerEmployeeNumbers);

        return roles;

        void Add(ProjectPortfolioRole role, IReadOnlyList<string> employeeNumbers)
        {
            if (employeeNumbers.Count == 0)
                return;

            roles.Add(role, [.. employeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])]);
        }
    }

    private static IEnumerable<string> RoleEmployeeNumbers(ImportProjectPortfolioDto row) =>
        row.SponsorEmployeeNumbers.Concat(row.OwnerEmployeeNumbers).Concat(row.ManagerEmployeeNumbers);

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
