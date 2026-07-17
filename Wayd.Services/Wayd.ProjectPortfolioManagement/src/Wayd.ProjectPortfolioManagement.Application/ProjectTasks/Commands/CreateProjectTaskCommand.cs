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
    List<Guid>? AssigneeIds
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
    }
}

public sealed class CreateProjectTaskCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<CreateProjectTaskCommandHandler> logger)
    : ICommandHandler<CreateProjectTaskCommand, ProjectTaskIdAndKey>
{
    private const string AppRequestName = nameof(CreateProjectTaskCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<CreateProjectTaskCommandHandler> _logger = logger;

    public async Task<Result<ProjectTaskIdAndKey>> Handle(CreateProjectTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Load the project with phases and tasks so the domain can resolve parentId
            var project = await _ppmDbContext.Projects
                .Include(p => p.Phases)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project with Id {ProjectId} not found.", request.ProjectId);
                return Result.Failure<ProjectTaskIdAndKey>("Project not found.");
            }

            var roles = GetRoles(request);

            // Get the next task key number atomically using a database query with row locking
            // This uses UPDLOCK and ROWLOCK to prevent race conditions when multiple tasks are created concurrently
            // Alternatively, we would need to include existing tasks in the project aggregate and get the number there
            var nextNumber = await _ppmDbContext.Database
                .SqlQuery<int>($@"
                    SELECT ISNULL(MAX([Number]), 0) + 1 AS [Value]
                    FROM [Ppm].[ProjectTasks] WITH (UPDLOCK, ROWLOCK)
                    WHERE [ProjectId] = {request.ProjectId}")
                .FirstOrDefaultAsync(cancellationToken);

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

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            var task = createResult.Value;

            _logger.LogInformation("Project task {TaskId} created with Key {TaskKey} for project {ProjectId}.",
                task.Id, task.Key.Value, request.ProjectId);

            return Result.Success(new ProjectTaskIdAndKey(task.Id, task.Key.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ProjectTaskIdAndKey>($"Error handling {AppRequestName} command.");
        }
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
