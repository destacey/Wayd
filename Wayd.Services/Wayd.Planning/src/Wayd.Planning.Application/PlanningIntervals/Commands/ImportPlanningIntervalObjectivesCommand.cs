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
/// Each row names its own planning interval, so one file can cover many — an onboarding customer arrives
/// with dozens of intervals, and one file per interval would make them split their history to fit.
/// </remarks>
public sealed record ImportPlanningIntervalObjectivesCommand(
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
