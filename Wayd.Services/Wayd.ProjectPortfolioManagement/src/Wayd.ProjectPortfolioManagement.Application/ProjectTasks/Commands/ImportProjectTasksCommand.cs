using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Additively imports a batch of project tasks and milestones, each created through its project so the
/// aggregate assigns keys, ordering and rolled-up phase dates exactly as it would for a task created in the
/// UI.
/// <para>
/// Rows are applied parents-before-children rather than in file order, so a batch can describe a whole work
/// breakdown without being pre-sorted. Tasks are named within their project, which is what lets a child row
/// point at its parent by name; a cycle or an unresolved parent fails the batch.
/// </para>
/// </summary>
public sealed record ImportProjectTasksCommand : ICommand
{
    public ImportProjectTasksCommand(IEnumerable<ImportProjectTaskDto> tasks)
    {
        Tasks = [.. tasks];
    }

    public List<ImportProjectTaskDto> Tasks { get; }
}

public sealed class ImportProjectTasksCommandValidator : CustomValidator<ImportProjectTasksCommand>
{
    public ImportProjectTasksCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Tasks)
            .NotNull()
            .NotEmpty();

        RuleForEach(t => t.Tasks)
            .NotNull()
            .SetValidator(new ImportProjectTaskDtoValidator());
    }
}

public sealed class ImportProjectTasksCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportProjectTasksCommandHandler> logger) : ICommandHandler<ImportProjectTasksCommand>
{
    private const string RequestName = nameof(ImportProjectTasksCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportProjectTasksCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProjectTasksCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var duplicates = request.Tasks
                .GroupBy(t => (t.ProjectKey.Value, Name: Normalize(t.Name)), TaskNameComparer.Instance)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.Value}: '{g.Key.Name}'")
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following task names appear more than once within their project: {string.Join(", ", duplicates)}.");

            var projectsResult = await ResolveProjects(request, cancellationToken);
            if (projectsResult.IsFailure)
                return Result.Failure(projectsResult.Error);
            var projectsByKey = projectsResult.Value;

            var employeeNumbers = request.Tasks
                .SelectMany(t => t.AssigneeEmployeeNumbers)
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var employeeIdsByNumber = await ResolveEmployees(employeeNumbers, cancellationToken);

            var unresolvedEmployees = employeeNumbers.Where(n => !employeeIdsByNumber.ContainsKey(n)).ToList();
            if (unresolvedEmployees.Count > 0)
                return Fail($"Could not resolve the following employee numbers: {Quote(unresolvedEmployees)}.");

            var nextNumbers = await NextTaskNumbers(projectsByKey.Values, cancellationToken);

            foreach (var group in request.Tasks.GroupBy(t => t.ProjectKey.Value, StringComparer.OrdinalIgnoreCase))
            {
                var project = projectsByKey[group.Key];

                var orderedResult = OrderParentsFirst([.. group]);
                if (orderedResult.IsFailure)
                    return Result.Failure(orderedResult.Error);

                // Tasks are named within their project, so a child row resolves its parent against the
                // tasks this batch has already created for that same project.
                var createdByName = new Dictionary<string, ProjectTask>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in orderedResult.Value)
                {
                    var parentIdResult = ResolveParentId(project, row, createdByName);
                    if (parentIdResult.IsFailure)
                        return Result.Failure(parentIdResult.Error);

                    var plannedDateRange = row.PlannedStart is null || row.PlannedEnd is null
                        ? null
                        : new FlexibleDateRange(row.PlannedStart.Value, row.PlannedEnd.Value);

                    nextNumbers.TryGetValue(project.Id, out var nextNumber);

                    var createResult = project.CreateTask(
                        nextNumber,
                        Normalize(row.Name),
                        row.Description?.Trim(),
                        row.Type,
                        row.Status,
                        row.Priority,
                        row.Progress.HasValue ? new Progress(row.Progress.Value) : null,
                        parentIdResult.Value,
                        plannedDateRange,
                        row.PlannedDate,
                        row.EstimatedEffortHours,
                        BuildRoles(row, employeeIdsByNumber));
                    if (createResult.IsFailure)
                        return Fail($"Could not create task '{row.Name}' in project '{row.ProjectKey.Value}': {createResult.Error}");

                    nextNumbers[project.Id] = nextNumber + 1;
                    createdByName[Normalize(row.Name)] = createResult.Value;
                }
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} project task(s).", RequestName, request.Tasks.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Orders a project's rows so every parent is created before its children, letting a batch be authored
    /// in any order. Rows whose parent is not in the batch come first — their parent either already exists
    /// or is missing, which the per-row resolution reports. Rows left unplaced form a cycle.
    /// </summary>
    private Result<List<ImportProjectTaskDto>> OrderParentsFirst(List<ImportProjectTaskDto> rows)
    {
        var rowsByName = rows.ToDictionary(r => Normalize(r.Name), r => r, StringComparer.OrdinalIgnoreCase);

        var ordered = new List<ImportProjectTaskDto>(rows.Count);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var remaining = rows.ToList();
        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(r => r.ParentTaskName is null
                    || !rowsByName.ContainsKey(Normalize(r.ParentTaskName))
                    || placed.Contains(Normalize(r.ParentTaskName)))
                .ToList();

            if (ready.Count == 0)
            {
                var cycle = remaining.Select(r => $"'{r.Name}'");
                return Fail<List<ImportProjectTaskDto>>(
                    $"The following tasks in project '{rows[0].ProjectKey.Value}' form a parent cycle: {string.Join(", ", cycle)}.");
            }

            foreach (var row in ready)
            {
                ordered.Add(row);
                placed.Add(Normalize(row.Name));
                remaining.Remove(row);
            }
        }

        return Result.Success(ordered);
    }

    /// <summary>
    /// Resolves the id a row hangs off: its named parent task when it has one, otherwise the phase itself,
    /// which the aggregate reads as "root task in this phase". Parents may come from this batch or from
    /// tasks the project already has.
    /// </summary>
    private Result<Guid> ResolveParentId(Project project, ImportProjectTaskDto row, Dictionary<string, ProjectTask> createdByName)
    {
        if (row.ParentTaskName is not null)
        {
            var parentName = Normalize(row.ParentTaskName);

            if (createdByName.TryGetValue(parentName, out var batchParent))
                return Result.Success(batchParent.Id);

            var existing = project.Tasks
                .Where(t => string.Equals(t.Name, parentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return existing.Count switch
            {
                1 => Result.Success(existing[0].Id),
                0 => Fail<Guid>($"Could not resolve parent task '{row.ParentTaskName}' for task '{row.Name}' in project '{row.ProjectKey.Value}'."),
                _ => Fail<Guid>($"Parent task name '{row.ParentTaskName}' matches more than one task in project '{row.ProjectKey.Value}'."),
            };
        }

        var phaseName = Normalize(row.PhaseName);
        var phases = project.Phases
            .Where(p => string.Equals(p.Name, phaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return phases.Count switch
        {
            1 => Result.Success(phases[0].Id),
            0 => Fail<Guid>($"Could not resolve phase '{row.PhaseName}' for task '{row.Name}' in project '{row.ProjectKey.Value}'. The project's lifecycle determines its phases."),
            _ => Fail<Guid>($"Phase name '{row.PhaseName}' matches more than one phase in project '{row.ProjectKey.Value}'."),
        };
    }

    /// <summary>
    /// Loads every referenced project with the phases and tasks the aggregate needs to place new work.
    /// </summary>
    private async Task<Result<Dictionary<string, Project>>> ResolveProjects(ImportProjectTasksCommand request, CancellationToken cancellationToken)
    {
        // Key is persisted through a value converter, so compare against ProjectKey instances.
        var keys = request.Tasks.Select(t => t.ProjectKey).Distinct().ToList();

        var projects = await _projectPortfolioManagementDbContext.Projects
            .Include(p => p.Phases)
            .Include(p => p.Tasks)
            .Where(p => keys.Contains(p.Key))
            .ToListAsync(cancellationToken);

        var projectsByKey = projects.ToDictionary(p => p.Key.Value, p => p, StringComparer.OrdinalIgnoreCase);

        var unresolved = keys
            .Select(k => k.Value)
            .Where(k => !projectsByKey.ContainsKey(k))
            .ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, Project>>($"Could not resolve the following projects: {Quote(unresolved)}.");

        return Result.Success(projectsByKey);
    }

    private async Task<Dictionary<string, Guid>> ResolveEmployees(HashSet<string> employeeNumbers, CancellationToken cancellationToken)
    {
        if (employeeNumbers.Count == 0)
            return [];

        return (await _projectPortfolioManagementDbContext.Employees
                .Where(e => employeeNumbers.Contains(e.EmployeeNumber))
                .Select(e => new { e.Id, e.EmployeeNumber })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.EmployeeNumber, e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Seeds the per-project task number sequence from the highest number already used. The single-task
    /// handler takes a row lock for this because tasks can be created concurrently; an import applies its
    /// rows in one pass, so the running number is advanced in memory instead.
    /// </summary>
    private async Task<Dictionary<Guid, int>> NextTaskNumbers(IEnumerable<Project> projects, CancellationToken cancellationToken)
    {
        var projectIds = projects.Select(p => p.Id).ToList();

        var maxNumbers = (await _projectPortfolioManagementDbContext.ProjectTasks
                .Where(t => projectIds.Contains(t.ProjectId))
                .GroupBy(t => t.ProjectId)
                .Select(g => new { ProjectId = g.Key, MaxNumber = g.Max(t => t.Number) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ProjectId, x => x.MaxNumber);

        return projectIds.ToDictionary(id => id, id => maxNumbers.TryGetValue(id, out var max) ? max + 1 : 1);
    }

    private static Dictionary<TaskRole, HashSet<Guid>> BuildRoles(ImportProjectTaskDto row, Dictionary<string, Guid> employeeIdsByNumber)
    {
        if (row.AssigneeEmployeeNumbers.Count == 0)
            return [];

        return new Dictionary<TaskRole, HashSet<Guid>>
        {
            [TaskRole.Assignee] = [.. row.AssigneeEmployeeNumbers.Select(n => employeeIdsByNumber[Normalize(n)])],
        };
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private Result<T> Fail<T>(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure<T>(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));

    /// <summary>Compares (project key, task name) pairs case-insensitively on both parts.</summary>
    private sealed class TaskNameComparer : IEqualityComparer<(string Value, string Name)>
    {
        public static readonly TaskNameComparer Instance = new();

        public bool Equals((string Value, string Name) x, (string Value, string Name) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Value, y.Value)
            && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode((string Value, string Name) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
