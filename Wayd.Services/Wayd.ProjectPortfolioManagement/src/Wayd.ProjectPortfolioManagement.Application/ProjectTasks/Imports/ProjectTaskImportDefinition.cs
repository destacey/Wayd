using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;

/// <summary>
/// Imports project tasks and milestones into the stages of the projects that own them.
/// </summary>
/// <remarks>
/// Per group, keyed on the project, because rows within a project are not independent: a child row hangs off
/// a parent row in the same file, and the per-project task number sequence advances as rows are applied. A
/// partly applied breakdown is worse than none — the rows that landed are indistinguishable from tasks that
/// were always there — so one bad row keeps its project's whole tree out and leaves every other project alone.
/// <para>
/// <see cref="ImportPassScope.Chunked"/> is safe at that grouping and only at that grouping: the runner fills
/// a chunk with whole groups, so a project's rows are never split across two saves, and the number sequence
/// is re-seeded per chunk from what the previous one saved.
/// </para>
/// <para>
/// A project stops at its first rejection. The runner keeps the rest of its rows out regardless, and carrying
/// on would only add errors that follow from the first — a child reported as orphaned when its parent was the
/// row that actually failed.
/// </para>
/// </remarks>
public sealed class ProjectTaskImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProjectTaskDto>(serializer)
{
    public const string ImportKey = "ppm.project-tasks";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Project Tasks";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Projects;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "project";

    // Saving chunk by chunk makes a larger file possible, but a run at that size is not yet proven end to
    // end, so the cap stays where the all-or-nothing version had it. The preflight bound follows it, and
    // would have to anyway: a preflight is one transaction rolled back at the end, so it cannot release
    // its locks chunk by chunk the way a real run does — there, the file size is the lock time.
    public override int MaxRows => 10_000;

    // Canonical by construction: ProjectKey trims and uppercases, so two rows spelling a key differently
    // still land in the same group.
    protected override string? GroupKey(ImportProjectTaskDto row) => row.ProjectKey.Value;

    protected override IReadOnlyList<ImportPass<ImportProjectTaskDto>> Steps =>
    [
        new("CreateTasks", ImportPassScope.Chunked, CreateTasks),
    ];

    private async Task<Result> CreateTasks(ImportPassContext<ImportProjectTaskDto> context, CancellationToken cancellationToken)
    {
        var projectsByKey = await ResolveProjects(context, cancellationToken);
        var employeeIdsByNumber = await ResolveEmployees(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (!projectsByKey.ContainsKey(data.ProjectKey.Value))
                row.Failed($"No project was found with key '{data.ProjectKey.Value}'.");
            else if (RejectedAssignees(data, employeeIdsByNumber) is { } unresolved)
                row.Failed($"No employee was found with number {Quote(unresolved)}.");
        }

        var nextNumbers = await NextTaskNumbers(projectsByKey.Values, cancellationToken);

        foreach (var group in context.Accepted.GroupBy(r => r.Data.ProjectKey.Value, StringComparer.OrdinalIgnoreCase))
        {
            var project = projectsByKey[group.Key];

            // Tasks created by this run, keyed by the import id of the row that made them, so a child row
            // can hang off a parent row wherever it sits in the file.
            var createdByImportId = new Dictionary<string, ProjectTask>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in OrderParentsFirst([.. group]))
            {
                var data = row.Data;

                var parentId = ResolveParentId(project, row, createdByImportId);
                if (parentId.IsFailure)
                {
                    row.Failed(parentId.Error);
                    break;
                }

                var plannedDateRange = data.PlannedStart is null || data.PlannedEnd is null
                    ? null
                    : new FlexibleDateRange(data.PlannedStart.Value, data.PlannedEnd.Value);

                nextNumbers.TryGetValue(project.Id, out var nextNumber);

                var created = project.CreateTask(
                    nextNumber,
                    Normalize(data.Name),
                    data.Description?.Trim(),
                    data.Type,
                    data.Status,
                    data.Priority,
                    data.Progress.HasValue ? new Progress(data.Progress.Value) : null,
                    parentId.Value,
                    plannedDateRange,
                    data.PlannedDate,
                    data.EstimatedEffortHours,
                    BuildRoles(data, employeeIdsByNumber));
                if (created.IsFailure)
                {
                    row.Failed($"Could not create task '{data.Name}' in project '{group.Key}': {created.Error}");
                    break;
                }

                nextNumbers[project.Id] = nextNumber + 1;
                createdByImportId[row.ImportId] = created.Value;
                row.Created(created.Value.Id);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Orders a project's rows so every parent is created before its children, letting a file be authored
    /// in any order.
    /// </summary>
    /// <remarks>
    /// Rows whose parent is not in the file come first — their parent either already exists or is missing,
    /// which the per-row resolution reports. Rows left unplaced form a cycle and are returned last, so
    /// each is rejected by name against the parent it could not resolve rather than failing the pass.
    /// </remarks>
    private static List<ImportRowItem<ImportProjectTaskDto>> OrderParentsFirst(
        List<ImportRowItem<ImportProjectTaskDto>> rows)
    {
        var byImportId = rows.ToDictionary(r => r.ImportId, StringComparer.OrdinalIgnoreCase);

        var ordered = new List<ImportRowItem<ImportProjectTaskDto>>(rows.Count);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = rows.ToList();

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(r => r.Data.ParentImportId is not { } parent
                    || !byImportId.ContainsKey(parent.Trim())
                    || placed.Contains(parent.Trim()))
                .ToList();

            if (ready.Count == 0)
            {
                ordered.AddRange(remaining);
                break;
            }

            foreach (var row in ready)
            {
                ordered.Add(row);
                placed.Add(row.ImportId);
                remaining.Remove(row);
            }
        }

        return ordered;
    }

    /// <summary>
    /// Resolves the id a row hangs off: its parent task when it names one, otherwise the stage itself,
    /// which the aggregate reads as "root task in this stage".
    /// </summary>
    private static Result<Guid> ResolveParentId(
        Project project,
        ImportRowItem<ImportProjectTaskDto> row,
        Dictionary<string, ProjectTask> createdByImportId)
    {
        var data = row.Data;

        if (data.ParentImportId is { } parentImportId)
        {
            return createdByImportId.TryGetValue(parentImportId.Trim(), out var parent)
                ? Result.Success(parent.Id)
                : Result.Failure<Guid>($"No task in this file has import id '{parentImportId}', named as the parent of '{data.Name}'.");
        }

        if (data.ParentTaskId is { } parentTaskId)
        {
            return project.Tasks.Any(t => t.Id == parentTaskId)
                ? Result.Success(parentTaskId)
                : Result.Failure<Guid>($"No task was found with id '{parentTaskId}' in project '{data.ProjectKey.Value}'.");
        }

        var stageName = Normalize(data.StageName);
        var stages = project.Stages
            .Where(s => string.Equals(s.Name, stageName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return stages.Count switch
        {
            1 => Result.Success(stages[0].Id),
            0 => Result.Failure<Guid>($"No stage named '{data.StageName}' was found in project '{data.ProjectKey.Value}'. The project's lifecycle determines its stages."),
            _ => Result.Failure<Guid>($"Stage name '{data.StageName}' matches more than one stage in project '{data.ProjectKey.Value}'."),
        };
    }

    /// <summary>Loads each referenced project with the stages and tasks the aggregate needs to place new work.</summary>
    private async Task<Dictionary<string, Project>> ResolveProjects(
        ImportPassContext<ImportProjectTaskDto> context, CancellationToken cancellationToken)
    {
        // Key is persisted through a value converter, so compare against ProjectKey instances: the
        // property translates but a member of it does not.
        var keys = context.Rows.Select(r => r.Data.ProjectKey).Distinct().ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Stages)
                .Include(p => p.Tasks)
                .Where(p => keys.Contains(p.Key))
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Key.Value, p => p, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(
        ImportPassContext<ImportProjectTaskDto> context, CancellationToken cancellationToken)
    {
        var employeeNumbers = context.Rows
            .SelectMany(r => r.Data.AssigneeEmployeeNumbers)
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (employeeNumbers.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        return (await _projectPortfolioManagementDbContext.Employees
                .AsNoTracking()
                .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
                .Select(e => new { e.Id, e.EmployeeNumber })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Seeds the per-project task number sequence from the highest number already used.
    /// </summary>
    /// <remarks>
    /// The single-task handler takes a row lock for this because tasks can be created concurrently; a chunk
    /// applies its rows before any of them is saved, so the running number is advanced in memory instead. A
    /// project is never split across chunks, so the seed read here is always of what an earlier chunk saved.
    /// </remarks>
    private async Task<Dictionary<Guid, int>> NextTaskNumbers(
        IEnumerable<Project> projects, CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(p => p.Id).ToList();

        var maxNumbers = (await _projectPortfolioManagementDbContext.ProjectTasks
                .AsNoTracking()
                .Where(t => projectIds.Contains(t.ProjectId))
                .GroupBy(t => t.ProjectId)
                .Select(g => new { ProjectId = g.Key, MaxNumber = g.Max(t => t.Number) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ProjectId, x => x.MaxNumber);

        return projectIds.ToDictionary(id => id, id => maxNumbers.TryGetValue(id, out var max) ? max + 1 : 1);
    }

    private static List<string>? RejectedAssignees(
        ImportProjectTaskDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        var unresolved = data.AssigneeEmployeeNumbers
            .Select(Normalize)
            .Where(n => !employeeIdsByNumber.ContainsKey(n))
            .Distinct()
            .ToList();

        return unresolved.Count == 0 ? null : unresolved;
    }

    private static Dictionary<TaskRole, HashSet<Guid>> BuildRoles(
        ImportProjectTaskDto data, Dictionary<string, Guid> employeeIdsByNumber)
    {
        if (data.AssigneeEmployeeNumbers.Count == 0)
            return [];

        return new Dictionary<TaskRole, HashSet<Guid>>
        {
            [TaskRole.Assignee] = [.. data.AssigneeEmployeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])],
        };
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
