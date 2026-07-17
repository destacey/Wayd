namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Represents a command to update the placement of a task within a project, including its parent and/or order among
/// sibling tasks.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project containing the task.</param>
/// <param name="TaskId">The unique identifier of the task to update.</param>
/// <param name="ParentId">The unique identifier of the parent phase or task. If it matches a phase, the task becomes a root task in that phase. If it matches a task, the task becomes a child of that task.</param>
/// <param name="Order">The one-based position to assign to the task among its siblings, or null to set the order last within the parent.</param>
public sealed record UpdateProjectTaskPlacementCommand(Guid ProjectId, Guid TaskId, Guid ParentId, int? Order) : ICommand;

public sealed class UpdateProjectTaskPlacementCommandValidator : CustomValidator<UpdateProjectTaskPlacementCommand>
{
    public UpdateProjectTaskPlacementCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.TaskId)
            .NotEmpty();

        RuleFor(x => x.ParentId)
            .NotEmpty()
            .WithMessage("ParentId is required and cannot be an empty GUID.");

        RuleFor(x => x.Order)
            .GreaterThan(0)
                .When(x => x.Order.HasValue)
                .WithMessage("Order must be greater than 0 when specified.");
    }
}

public sealed class UpdateProjectTaskPlacementCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<UpdateProjectTaskPlacementCommandHandler> logger)
    : ICommandHandler<UpdateProjectTaskPlacementCommand>
{
    private const string AppRequestName = nameof(UpdateProjectTaskPlacementCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<UpdateProjectTaskPlacementCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectTaskPlacementCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var taskExists = await _ppmDbContext.ProjectTasks
            .AnyAsync(t => t.Id == request.TaskId, cancellationToken);
            if (!taskExists)
            {
                _logger.LogInformation("Project Task {TaskId} not found.", request.TaskId);
                return Result.Failure($"Project Task not found.");
            }

            var project = await _ppmDbContext.Projects
                .Include(p => p.Phases)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
                return Result.Failure($"Project {request.ProjectId} not found.");
            }

            var changeResult = project.ChangeTaskPlacement(request.TaskId, request.ParentId, request.Order);
            if (changeResult.IsFailure)
            {
                _logger.LogError("Error changing placement for task {TaskId}. Error: {Error}", request.TaskId, changeResult.Error);
                return changeResult;
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully changed placement for task {TaskId} and parent {ParentId}.", request.TaskId, request.ParentId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
