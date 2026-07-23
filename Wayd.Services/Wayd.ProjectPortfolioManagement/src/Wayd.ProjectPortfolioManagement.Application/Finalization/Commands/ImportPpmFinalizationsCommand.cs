using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Finalization.Commands;

/// <summary>
/// Closes out programs and portfolios once their contents have been imported — the last step of a PPM
/// import, and the only one that is not additive.
/// <para>
/// It exists because the domain's guards run in opposite directions: things can only be added to an
/// <i>active</i> program or portfolio, but one can only be closed when everything inside it is already
/// closed. Historical work is therefore imported active and finished here.
/// </para>
/// <para>
/// Rows are applied programs-first regardless of the order they appear in, since a portfolio cannot close
/// while one of its programs is still open. The batch is all-or-nothing.
/// </para>
/// </summary>
public sealed record ImportPpmFinalizationsCommand : ICommand
{
    public ImportPpmFinalizationsCommand(IEnumerable<FinalizePpmItemDto> items)
    {
        Items = [.. items];
    }

    public List<FinalizePpmItemDto> Items { get; }
}

public sealed class ImportPpmFinalizationsCommandValidator : CustomValidator<ImportPpmFinalizationsCommand>
{
    public ImportPpmFinalizationsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Items)
            .NotNull()
            .NotEmpty();

        RuleForEach(i => i.Items)
            .NotNull()
            .SetValidator(new FinalizePpmItemDtoValidator());
    }
}

public sealed class ImportPpmFinalizationsCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportPpmFinalizationsCommandHandler> logger) : ICommandHandler<ImportPpmFinalizationsCommand>
{
    private const string RequestName = nameof(ImportPpmFinalizationsCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportPpmFinalizationsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportPpmFinalizationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Every portfolio named by the batch is loaded with the programs and projects the closing
            // guards read, whether it is being closed itself or merely owns a program that is.
            var portfolioNames = request.Items
                .Select(i => Normalize(i.Type is FinalizePpmItemType.Portfolio ? i.Name : i.PortfolioName!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var portfolios = await _projectPortfolioManagementDbContext.Portfolios
                .Include(p => p.Programs)
                    .ThenInclude(p => p.Projects)
                .Include(p => p.Projects)
                .Where(p => portfolioNames.Contains(p.Name))
                .ToListAsync(cancellationToken);

            var ambiguous = portfolios
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (ambiguous.Count > 0)
                return Fail($"The following portfolio names match more than one portfolio: {Quote(ambiguous)}.");

            var portfoliosByName = portfolios.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            var unresolved = portfolioNames.Where(n => !portfoliosByName.ContainsKey(n)).ToList();
            if (unresolved.Count > 0)
                return Fail($"Could not resolve the following portfolios: {Quote(unresolved)}.");

            // Programs first: a portfolio cannot close while one of its programs is still open, so the row
            // order in the file must not decide the outcome.
            foreach (var row in request.Items.Where(i => i.Type is FinalizePpmItemType.Program))
            {
                var result = FinalizeProgram(portfoliosByName[Normalize(row.PortfolioName!)], row);
                if (result.IsFailure)
                    return result;
            }

            foreach (var row in request.Items.Where(i => i.Type is FinalizePpmItemType.Portfolio))
            {
                var result = FinalizePortfolio(portfoliosByName[Normalize(row.Name)], row);
                if (result.IsFailure)
                    return result;
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: finalized {Count} item(s).", RequestName, request.Items.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    private Result FinalizeProgram(ProjectPortfolio portfolio, FinalizePpmItemDto row)
    {
        var matches = portfolio.Programs
            .Where(p => string.Equals(p.Name, Normalize(row.Name), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return Fail($"Could not resolve program '{row.Name}' in portfolio '{row.PortfolioName}'.");
        if (matches.Count > 1)
            return Fail($"Program name '{row.Name}' matches more than one program in portfolio '{row.PortfolioName}'.");

        var program = matches[0];
        var result = row.Status is FinalizePpmItemStatus.Cancelled
            ? program.Cancel()
            : program.Complete();

        return result.IsFailure
            ? Fail($"Could not finalize program '{row.Name}' as {row.Status}: {result.Error}")
            : Result.Success();
    }

    private Result FinalizePortfolio(ProjectPortfolio portfolio, FinalizePpmItemDto row)
    {
        var close = portfolio.Close(row.EndDate!.Value);
        if (close.IsFailure)
            return Fail($"Could not close portfolio '{row.Name}': {close.Error}");

        if (row.Status is not FinalizePpmItemStatus.Archived)
            return Result.Success();

        var archive = portfolio.Archive();

        return archive.IsFailure
            ? Fail($"Could not archive portfolio '{row.Name}': {archive.Error}")
            : Result.Success();
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
