using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

/// <summary>
/// Submits a file of strategic initiatives to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the initiative file and the optional KPI
/// file, checks the permission, validates each row's shape, groups the KPI rows onto the initiative row
/// they name, then hands the parsed rows here.
/// </remarks>
public sealed record ImportStrategicInitiativesCommand(
    IReadOnlyList<SubmittedImportRow<ImportStrategicInitiativeDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// Initiative names are unique, so a file repeating one could never apply whole. The definition checks
/// each row against what already exists; only this can see the file as a set.
/// </remarks>
public sealed class ImportStrategicInitiativesCommandValidator : CustomValidator<ImportStrategicInitiativesCommand>
{
    public ImportStrategicInitiativesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows.Select(r => r.Data.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Count)
                .WithMessage("Strategic initiative Name must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportStrategicInitiativeDtoValidator()));
    }
}

public sealed class ImportStrategicInitiativesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportStrategicInitiativesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportStrategicInitiativesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no strategic initiatives.");

        var definition = _registry.Find(StrategicInitiativeImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(StrategicInitiativeImportDefinition.ImportKey, rows), cancellationToken);
    }
}
