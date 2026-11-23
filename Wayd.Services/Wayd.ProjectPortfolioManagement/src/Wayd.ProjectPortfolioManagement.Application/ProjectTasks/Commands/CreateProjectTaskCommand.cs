using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

public sealed record CreateProjectTaskCommand(
    Guid ProjectId,
    string Name,
    string? Description,
    ProjectTaskType Type,
    Domain.Enums.TaskStatus Status,
    TaskPriority Priority,
    Progress? Progress,
    Guid ParentId,
    FlexibleDateRange? PlannedDateRange,
    LocalDate? PlannedDate,
    decimal? EstimatedEffortHours,
    List<Guid>? AssigneeIds,
    List<Guid>? DeliverableIds = null,
    int? Order = null
) : ICommand<ProjectTaskIdAndKey>;

public sealed class CreateProjectTaskCommandValidator : AbstractValidator<CreateProjectTaskCommand>
{
    public CreateProjectTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Priority)
            .IsInEnum();

        RuleFor(x => x.Progress)
            .NotNull()
            .When(x => x.Type == ProjectTaskType.Task)
            .WithMessage("Progress is required for tasks.");

        RuleFor(x => x.Progress)
            .Null()
            .When(x => x.Type == ProjectTaskType.Milestone)
            .WithMessage("Progress is not applicable for milestones.");

        RuleFor(x => x.ParentId)
            .NotEmpty()
            .WithMessage("ParentId is required and cannot be an empty GUID.");

        // Milestone-specific validations
        RuleFor(x => x.PlannedDate)
            .NotNull()
            .When(x => x.Type == ProjectTaskType.Milestone)
            .WithMessage("PlannedDate is required for milestones.");

        RuleFor(x => x.PlannedDateRange)
            .Null()
            .When(x => x.Type == ProjectTaskType.Milestone)
            .WithMessage("Milestones cannot have a date range.");

        // Task-specific validations
        RuleFor(x => x.PlannedDate)
            .Null()
            .When(x => x.Type == ProjectTaskType.Task)
            .WithMessage("Tasks cannot have a single planned date. Use PlannedDateRange instead.");

        RuleFor(x => x.EstimatedEffortHours)
            .GreaterThan(0)
            .When(x => x.EstimatedEffortHours.HasValue);

        RuleFor(x => x.AssigneeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("AssigneeIds cannot contain empty GUIDs.");

        RuleFor(x => x.DeliverableIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("DeliverableIds cannot contain empty GUIDs.");

        RuleFor(x => x.Order)
            .GreaterThan(0)
            .When(x => x.Order.HasValue);
    }
}

public sealed class CreateProjectTaskCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<CreateProjectTaskCommandHandler> logger)
    : ICommandHandler<CreateProjectTaskCommand, ProjectTaskIdAndKey>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<CreateProjectTaskCommandHandler> _logger = logger;

    public async Task<Result<ProjectTaskIdAndKey>> Handle(CreateProjectTaskCommand request, CancellationToken cancellationToken)
    {
        var deliverableIds = request.DeliverableIds?.Distinct().ToList() ?? [];

        // The number is MAX + 1, so creates on one project take turns: each holds an update lock on the project's
        // row until its task is saved, which works for a project with no tasks yet and locks no task rows that a
        // reorder or delete could be waiting on. The MAX is a separate statement because under read committed
        // snapshot a statement reads as of when it started, and only one started after the lock sees the task the
        // previous create saved. The project loads after it for the same reason.
        await using var unitOfWork = await _ppmDbContext.BeginUnitOfWork(cancellationToken);

        await _ppmDbContext.Database
            .SqlQuery<Guid>($@"
                SELECT [Id] AS [Value]
                FROM [Ppm].[Projects] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {request.ProjectId}")
            .ToListAsync(cancellationToken);

        var nextNumber = await _ppmDbContext.Database
            .SqlQuery<int>($@"
                SELECT ISNULL(MAX([Number]), 0) + 1 AS [Value]
                FROM [Ppm].[ProjectTasks]
                WHERE [ProjectId] = {request.ProjectId}")
            .FirstOrDefaultAsync(cancellationToken);

        // Load the project with stages and tasks so the domain can resolve parentId, and only the named
        // deliverables to link the task to.
        var project = await _ppmDbContext.Projects
            .AsSplitQuery()
            .Include(p => p.Stages)
            .Include(p => p.Tasks)
            .Include(p => p.Deliverables.Where(d => deliverableIds.Contains(d.Id)))
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null)
        {
            _logger.LogInformation("Project with Id {ProjectId} not found.", request.ProjectId);
            return Result.Failure<ProjectTaskIdAndKey>("Project not found.");
        }

        var roles = GetRoles(request);
        var peopleExist = await _ppmDbContext.EnsureEmployeesExist(roles.Values.SelectMany(ids => ids), cancellationToken);
        if (peopleExist.IsFailure)
            return Result.Failure<ProjectTaskIdAndKey>(peopleExist.Error);

        // Create the task through the project
        var createResult = project.CreateTask(
            nextNumber,
            request.Name,
            request.Description,
            request.Type,
            request.Status,
            request.Priority,
            request.Progress,
            request.ParentId,
            request.PlannedDateRange,
            request.PlannedDate,
            request.EstimatedEffortHours,
            roles
        );

        if (createResult.IsFailure)
        {
            _logger.LogError("Error creating task {TaskName} for project {ProjectId}. Error: {Error}",
                request.Name, request.ProjectId, createResult.Error);
            return Result.Failure<ProjectTaskIdAndKey>(createResult.Error);
        }

        var task = createResult.Value;

        // Created last among its siblings, then moved into place in the same save, so no reader sees it last.
        if (request.Order.HasValue)
        {
            var placeResult = project.ChangeTaskPlacement(task.Id, request.ParentId, request.Order);
            if (placeResult.IsFailure)
                return Result.Failure<ProjectTaskIdAndKey>(placeResult.Error);
        }

        if (deliverableIds.Count > 0)
        {
            var linkResult = project.SetTaskDeliverables(task.Id, deliverableIds);
            if (linkResult.IsFailure)
                return Result.Failure<ProjectTaskIdAndKey>(linkResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Project task {TaskId} created with Key {TaskKey} for project {ProjectId}.",
            task.Id, task.Key.Value, request.ProjectId);

        return Result.Success(new ProjectTaskIdAndKey(task.Id, task.Key.Value));
    }

    private static Dictionary<TaskRole, HashSet<Guid>> GetRoles(CreateProjectTaskCommand request)
    {
        Dictionary<TaskRole, HashSet<Guid>> roles = [];

        if (request.AssigneeIds != null && request.AssigneeIds.Count != 0)
        {
            roles.Add(TaskRole.Assignee, [.. request.AssigneeIds]);
        }

        return roles;
    }
}
