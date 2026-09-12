using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

/// <summary>
/// Submits a file of planning interval objectives to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import, and the first one with a rule of its own: a file must belong
/// to a single planning interval. That was previously enforced in the controller by comparing each row
/// against the route id and answering with a route-parameter mismatch, which described the HTTP shape of
/// the request rather than the rule.
/// </remarks>
public sealed record ImportPlanningIntervalObjectivesCommand(
    Guid PlanningIntervalId,
    IReadOnlyList<SubmittedImportRow<ImportPlanningIntervalObjectiveDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, not the file they arrived in.
/// </summary>
/// <remarks>
/// On the handler pipeline, so the check holds for any caller rather than only the one endpoint that
/// happens to validate its request model first. A controller's validator covers the CSV shape it parsed;
/// this covers what the import is actually being asked to apply.
/// </remarks>
public sealed class ImportPlanningIntervalObjectivesCommandValidator
    : CustomValidator<ImportPlanningIntervalObjectivesCommand>
{
    public ImportPlanningIntervalObjectivesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PlanningIntervalId)
            .NotEmpty();

        RuleFor(c => c.Rows)
            .NotEmpty();

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data)
                .NotNull()
                .SetValidator(new ImportPlanningIntervalObjectiveDtoValidator()));
    }
}

public sealed class ImportPlanningIntervalObjectivesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportPlanningIntervalObjectivesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(
        ImportPlanningIntervalObjectivesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no objectives.");

        // One file, one planning interval. The definition resolves each row's interval independently and
        // would happily apply a mixed file, so this is the rule rather than a limitation: an import of
        // objectives is submitted against an interval, and a row naming a different one is a mistake in
        // the file, not an instruction to spread the import across two.
        var foreign = command.Rows
            .Select((row, index) => (Row: row, Number: index + 1))
            .FirstOrDefault(r => r.Row.Data.PlanningIntervalId != command.PlanningIntervalId);

        if (foreign.Row is not null)
        {
            var key = SubmittedImportRow.KeyFor(foreign.Row.ImportId, foreign.Number);

            return Result.Failure<Guid>(
                $"Row '{key}' names planning interval '{foreign.Row.Data.PlanningIntervalId}', but this import is for '{command.PlanningIntervalId}'. A file must belong to one planning interval.");
        }

        var definition = _registry.Find(PlanningIntervalObjectiveImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(PlanningIntervalObjectiveImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
