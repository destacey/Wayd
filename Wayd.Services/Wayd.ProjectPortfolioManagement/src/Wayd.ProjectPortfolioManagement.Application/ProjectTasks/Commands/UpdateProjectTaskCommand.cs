using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

public sealed record UpdateProjectTaskCommand(
    Guid Id,
    string Name,
    string? Description,
    Domain.Enums.TaskStatus Status,
    TaskPriority Priority,
    Progress? Progress,
    Guid ParentId,
    FlexibleDateRange? PlannedDateRange,
    LocalDate? PlannedDate,
    decimal? EstimatedEffortHours,
    List<Guid>? AssigneeIds
) : ICommand;

public sealed class UpdateProjectTaskCommandValidator : AbstractValidator<UpdateProjectTaskCommand>
{
    public UpdateProjectTaskCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Priority)
            .IsInEnum();

        RuleFor(x => x.ParentId)
            .NotEmpty()
            .WithMessage("ParentId is required and cannot be an empty GUID.");

        RuleFor(x => x.EstimatedEffortHours)
            .GreaterThan(0)
            .When(x => x.EstimatedEffortHours.HasValue);

        RuleFor(x => x.AssigneeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("AssigneeIds cannot contain empty GUIDs.");
    }
}

public sealed class UpdateProjectTaskCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<UpdateProjectTaskCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateProjectTaskCommand>
{
    private const string AppRequestName = nameof(UpdateProjectTaskCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<UpdateProjectTaskCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateProjectTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _ppmDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Phases)
                .Include(p => p.Tasks)
                .ThenInclude(t => t.Roles)
                .FirstOrDefaultAsync(p => p.Tasks.Any(t => t.Id == request.Id), cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project task {TaskId} not found.", request.Id);
                return Result.Failure("Project task not found.");
            }

            var task = project.Tasks.First(t => t.Id == request.Id);

            // Validate parent — ParentId can be a phase or task, domain handles full validation
            if (request.ParentId == request.Id)
            {
                _logger.LogInformation("Cannot set task {TaskId} as its own parent.", request.Id);
                return Result.Failure("A task cannot be its own parent.");
            }

            // Update basic details
            var detailsResult = task.UpdateDetails(request.Name, request.Description, request.Priority);
            if (detailsResult.IsFailure)
            {
                return await HandleDomainFailure(project, detailsResult, cancellationToken);
            }

            // Update status
            var statusResult = task.UpdateStatus(request.Status, _dateTimeProvider.Now);
            if (statusResult.IsFailure)
            {
                return await HandleDomainFailure(project, statusResult, cancellationToken);
            }

            // Update progress
            if (task.Type is ProjectTaskType.Task)
            {
                if (request.Progress is null)
                {
                    _logger.LogInformation("Progress must be provided for task type 'Task'.");
                    return Result.Failure("Progress must be provided for task type 'Task'.");
                }

                var progressResult = task.UpdateProgress(request.Progress);
                if (progressResult.IsFailure)
                {
                    return await HandleDomainFailure(project, progressResult, cancellationToken);
                }
            }

            // Update effort
            var effortResult = task.UpdateEffort(request.EstimatedEffortHours);
            if (effortResult.IsFailure)
            {
                return await HandleDomainFailure(project, effortResult, cancellationToken);
            }

            // Update parent/phase if changed
            // For root tasks, the effective parent is the phase; for child tasks, it's the parent task
            var effectiveCurrentParentId = task.ParentId ?? task.ProjectPhaseId;
            var parentChanging = request.ParentId != effectiveCurrentParentId;

            // Update planned dates
            var plannedDatesResult = project.UpdateTaskDates(task.Id, request.PlannedDateRange, request.PlannedDate, parentChanging);
            if (plannedDatesResult.IsFailure)
            {
                return await HandleDomainFailure(project, plannedDatesResult, cancellationToken);
            }

            if (parentChanging)
            {
                var parentResult = project.ChangeTaskPlacement(task.Id, request.ParentId, null);
                if (parentResult.IsFailure)
                {
                    return await HandleDomainFailure(project, parentResult, cancellationToken);
                }
            }

            var roles = GetRoles(request);
            var updateRolesResult = task.UpdateRoles(roles);
            if (updateRolesResult.IsFailure)
            {
                return await HandleDomainFailure(project, updateRolesResult, cancellationToken);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project task {TaskId} updated successfully.", task.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private async Task<HashSet<Guid>> GetDescendantIds(Guid taskId, CancellationToken cancellationToken)
    {
        var descendants = new HashSet<Guid>();
        var children = await _ppmDbContext.ProjectTasks
            .Where(t => t.ParentId == taskId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var childId in children)
        {
            descendants.Add(childId);
            var childDescendants = await GetDescendantIds(childId, cancellationToken);
            descendants.UnionWith(childDescendants);
        }

        return descendants;
    }

    private static Dictionary<TaskRole, HashSet<Guid>> GetRoles(UpdateProjectTaskCommand request)
    {
        Dictionary<TaskRole, HashSet<Guid>> roles = [];

        if (request.AssigneeIds != null && request.AssigneeIds.Count != 0)
        {
            roles.Add(TaskRole.Assignee, [.. request.AssigneeIds]);
        }

        return roles;
    }

    private async Task<Result> HandleDomainFailure(Project project, Result errorResult, CancellationToken cancellationToken)
    {
        try
        {
            // Reset the project aggregate and all tasks/phases
            await _ppmDbContext.Entry(project).ReloadAsync(cancellationToken);
            foreach (var task in project.Tasks)
            {
                await _ppmDbContext.Entry(task).ReloadAsync(cancellationToken);
                task.ClearDomainEvents();
            }
            foreach (var phase in project.Phases)
            {
                await _ppmDbContext.Entry(phase).ReloadAsync(cancellationToken);
                phase.ClearDomainEvents();
            }
        }
        catch (NotImplementedException)
        {
            // Fallback for FakeProjectPortfolioManagementDbContext in unit tests
            foreach (var task in project.Tasks)
            {
                task.ClearDomainEvents();
            }
            foreach (var phase in project.Phases)
            {
                phase.ClearDomainEvents();
            }
        }

        _logger.LogError("Unable to update project task. Error message: {Error}", errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
