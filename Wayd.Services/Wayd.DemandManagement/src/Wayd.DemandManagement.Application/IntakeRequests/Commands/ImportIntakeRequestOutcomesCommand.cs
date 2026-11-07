using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Imports;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Submits a file of intake request outcomes to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import: a controller parses the file and checks each row's shape, then
/// hands the parsed rows here, where the definition that applies them is found and each payload stored.
/// </remarks>
public sealed record ImportIntakeRequestOutcomesCommand(
    IReadOnlyList<SubmittedImportRow<ImportIntakeRequestOutcomeDto>> Rows,
    Guid? SubmissionGroupId = null,
    bool ValidateOnly = false) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, on the handler pipeline, so the check holds for any caller rather than only
/// the endpoint that validates its request model first.
/// </summary>
public sealed class ImportIntakeRequestOutcomesCommandValidator : CustomValidator<ImportIntakeRequestOutcomesCommand>
{
    public ImportIntakeRequestOutcomesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Rows in different chunks only see each other's outcomes once the earlier chunk is saved, which a check
        // run never does, so a repeat is caught here for the whole file.
        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Where(r => r.Data is not null)
                .GroupBy(r => (r.Data.RequestId, r.Data.TargetId))
                .All(g => g.Count() == 1))
                .WithMessage("A request can point at each project or work item once, so no two rows may name the same request and target.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportIntakeRequestOutcomeDtoValidator()));
    }
}

public sealed class ImportIntakeRequestOutcomesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportIntakeRequestOutcomesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportIntakeRequestOutcomesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no intake request outcomes.");

        var definition = _registry.Find(IntakeRequestOutcomeImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(IntakeRequestOutcomeImportDefinition.ImportKey, rows, command.SubmissionGroupId, command.ValidateOnly),
            cancellationToken);
    }
}
