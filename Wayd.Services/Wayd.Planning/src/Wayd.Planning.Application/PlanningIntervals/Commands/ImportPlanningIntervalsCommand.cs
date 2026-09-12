using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

/// <summary>
/// Submits a file of planning intervals to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportPlanningIntervalsCommand(
    IReadOnlyList<SubmittedImportRow<ImportPlanningIntervalDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A planning interval's name is unique, so a file repeating one is ambiguous no matter what already
/// exists. The definition checks each row against what is already there; only this can see the file as a
/// set, so the two checks are complements rather than duplicates.
/// </remarks>
public sealed class ImportPlanningIntervalsCommandValidator : CustomValidator<ImportPlanningIntervalsCommand>
{
    public ImportPlanningIntervalsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Select(r => r.Data.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == rows.Count)
                .WithMessage("Planning interval Name must be unique within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data)
                .NotNull()
                .SetValidator(new ImportPlanningIntervalDtoValidator()));
    }
}

public sealed class ImportPlanningIntervalsCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportPlanningIntervalsCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(
        ImportPlanningIntervalsCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no planning intervals.");

        var definition = _registry.Find(PlanningIntervalImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(PlanningIntervalImportDefinition.ImportKey, rows, command.SubmissionGroupId),
            cancellationToken);
    }
}
