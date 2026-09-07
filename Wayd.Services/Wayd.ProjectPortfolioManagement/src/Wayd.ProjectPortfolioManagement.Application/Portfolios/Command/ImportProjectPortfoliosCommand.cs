using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

/// <summary>
/// Submits a file of portfolios to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProjectPortfoliosCommand(
    IReadOnlyList<SubmittedImportRow<ImportProjectPortfolioDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// Portfolio names are unique, so a file repeating one could never apply whole. The definition checks each
/// row against what already exists; only this can see the file as a set.
/// </remarks>
public sealed class ImportProjectPortfoliosCommandValidator : CustomValidator<ImportProjectPortfoliosCommand>
{
    public ImportProjectPortfoliosCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Portfolio Name must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProjectPortfolioDtoValidator()));
    }
}

public sealed class ImportProjectPortfoliosCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProjectPortfoliosCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProjectPortfoliosCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no portfolios.");

        var definition = _registry.Find(ProjectPortfolioImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProjectPortfolioImportDefinition.ImportKey, rows), cancellationToken);
    }
}
