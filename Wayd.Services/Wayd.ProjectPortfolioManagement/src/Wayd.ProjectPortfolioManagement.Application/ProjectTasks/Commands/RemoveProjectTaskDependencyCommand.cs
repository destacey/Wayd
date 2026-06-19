namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Removes (soft deletes) a dependency between two tasks.
/// </summary>
public sealed record RemoveProjectTaskDependencyCommand(Guid ProjectId, Guid PredecessorId, Guid SuccessorId) : ICommand;

public sealed class RemoveProjectTaskDependencyCommandValidator : CustomValidator<RemoveProjectTaskDependencyCommand>
{
    public RemoveProjectTaskDependencyCommandValidator()
    {
        RuleFor(x => x.PredecessorId)
            .NotEmpty();

        RuleFor(x => x.SuccessorId)
            .NotEmpty();
    }
}

internal sealed class RemoveProjectTaskDependencyCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<RemoveProjectTaskDependencyCommandHandler> logger)
    : ICommandHandler<RemoveProjectTaskDependencyCommand>
{
    private const string AppRequestName = nameof(RemoveProjectTaskDependencyCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RemoveProjectTaskDependencyCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveProjectTaskDependencyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var predecessor = await _ppmDbContext.ProjectTasks
            .Include(t => t.Successors)
            .FirstOrDefaultAsync(t => t.Id == request.PredecessorId && t.ProjectId == request.ProjectId, cancellationToken);
            if (predecessor is null)
            {
                _logger.LogWarning("Predecessor task {TaskId} not found", request.PredecessorId);
                return Result.Failure("Predecessor task not found");
            }

            var result = predecessor.RemoveDependency(request.SuccessorId, _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to remove dependency from {PredecessorId} to {SuccessorId}. Error: {Error}",
                    request.PredecessorId, request.SuccessorId, result.Error);
                return result;
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully removed dependency from {PredecessorId} to successor {SuccessorId}.",
                predecessor.Id, request.SuccessorId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
