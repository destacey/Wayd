using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Submits a file of project stage statuses to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProjectStagesCommand(
    IReadOnlyList<SubmittedImportRow<ImportProjectStageDto>> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the one thing that is true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A stage can only end up in one status, so two rows for the same stage contradict each other and the
/// file would apply whichever came last. The definition cannot see that — it applies rows one at a time.
/// </remarks>
public sealed class ImportProjectStagesCommandValidator : CustomValidator<ImportProjectStagesCommand>
{
    public ImportProjectStagesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Select(r => $"{r.Data.ProjectKey.Value}|{r.Data.StageName.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == rows.Count)
                .WithMessage("Each stage may appear only once within the file.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProjectStageDtoValidator()));
    }
}

public sealed class ImportProjectStagesCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProjectStagesCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProjectStagesCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no project stages.");

        var definition = _registry.Find(ProjectStageImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProjectStageImportDefinition.ImportKey, rows, command.SubmissionGroupId), cancellationToken);
    }
}
