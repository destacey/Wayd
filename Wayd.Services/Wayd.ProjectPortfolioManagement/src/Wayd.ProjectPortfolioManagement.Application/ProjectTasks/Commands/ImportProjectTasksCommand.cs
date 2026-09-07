using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Submits a file of project tasks to import, and answers with the id of the run.
/// </summary>
/// <remarks>
/// The application boundary for this import. A controller parses the file, checks the permission and
/// validates each row's shape, then hands the parsed rows here.
/// </remarks>
public sealed record ImportProjectTasksCommand(
    IReadOnlyList<SubmittedImportRow<ImportProjectTaskDto>> Rows) : ICommand<Guid>;

/// <summary>
/// Validates the rows themselves, and the two things that are true of a file rather than of any row.
/// </summary>
/// <remarks>
/// A task name is unique within its project, and a parent chain that loops back on itself can never be
/// ordered parents-first. Both are properties of the file as a set, which the definition cannot see: it
/// applies rows one at a time.
/// </remarks>
public sealed class ImportProjectTasksCommandValidator : CustomValidator<ImportProjectTasksCommand>
{
    public ImportProjectTasksCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Rows)
            .NotEmpty()
            .Must(rows => rows
                .Select(r => $"{r.Data.ProjectKey.Value}|{r.Data.Name.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == rows.Count)
                .WithMessage("Task Name must be unique within its project.")
            .Must(NoParentCycles)
                .WithMessage("Some tasks form a parent cycle, so they can never be created parents-first.");

        RuleForEach(c => c.Rows)
            .ChildRules(row => row.RuleFor(r => r.Data).NotNull().SetValidator(new ImportProjectTaskDtoValidator()));
    }

    /// <summary>
    /// Walks the in-file parent links, peeling off rows whose parent is outside the file or already
    /// placed. Anything left when nothing more can be placed is a cycle.
    /// </summary>
    private static bool NoParentCycles(IReadOnlyList<SubmittedImportRow<ImportProjectTaskDto>> rows)
    {
        var keys = new Dictionary<SubmittedImportRow<ImportProjectTaskDto>, string>();
        for (var i = 0; i < rows.Count; i++)
            keys[rows[i]] = SubmittedImportRow.KeyFor(rows[i].ImportId, i + 1);

        var present = keys.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = rows.ToList();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(r => r.Data.ParentImportId is not { } parent
                    || !present.Contains(parent.Trim())
                    || placed.Contains(parent.Trim()))
                .ToList();

            if (ready.Count == 0)
                return false;

            foreach (var row in ready)
            {
                placed.Add(keys[row]);
                remaining.Remove(row);
            }
        }

        return true;
    }
}

public sealed class ImportProjectTasksCommandHandler(
    IImportDefinitionRegistry registry,
    IDispatcher dispatcher) : ICommandHandler<ImportProjectTasksCommand, Guid>
{
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ImportProjectTasksCommand command, CancellationToken cancellationToken)
    {
        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no project tasks.");

        var definition = _registry.Find(ProjectTaskImportDefinition.ImportKey);
        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        // The definition owns how a row is stored, so the same payload shape reaches the runner whether it
        // applies now or days later after a resume.
        var rows = command.Rows
            .Select(r => new SubmittedImportRow(r.ImportId, definition.Value.SerializeRow(r.Data)))
            .ToList();

        return await _dispatcher.Send(
            new SubmitImportCommand(ProjectTaskImportDefinition.ImportKey, rows), cancellationToken);
    }
}
