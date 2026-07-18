namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

public sealed record DeleteProjectTaskCommand(Guid Id) : ICommand;

public sealed class DeleteProjectTaskCommandValidator : AbstractValidator<DeleteProjectTaskCommand>
{
    public DeleteProjectTaskCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProjectTaskCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<DeleteProjectTaskCommandHandler> logger)
    : ICommandHandler<DeleteProjectTaskCommand>
{
    private const string AppRequestName = nameof(DeleteProjectTaskCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<DeleteProjectTaskCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProjectTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _ppmDbContext.ProjectTasks
                .Include(t => t.Children)
                .Include(t => t.Predecessors)
                .Include(t => t.Successors)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
            if (task is null)
            {
                _logger.LogInformation("Project task {TaskId} not found.", request.Id);
                return Result.Failure("Project task not found.");
            }

            // Load the project to call DeleteTask
            var project = await _ppmDbContext.Projects
                .Include(p => p.Tasks.Where(t => t.ParentId == task.ParentId))
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found for task {TaskId}.", task.ProjectId, request.Id);
                return Result.Failure("Project not found.");
            }

            // Attempt to delete through the project aggregate
            var deleteResult = project.DeleteTask(request.Id);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Error deleting task {TaskId}. Error: {Error}", request.Id, deleteResult.Error);
                return deleteResult;
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project task {TaskId} deleted successfully.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
