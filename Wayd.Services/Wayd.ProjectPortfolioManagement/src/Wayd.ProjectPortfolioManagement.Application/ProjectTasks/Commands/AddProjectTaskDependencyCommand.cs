namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Adds a dependency between two tasks (Finish-to-Start).
/// </summary>
public sealed record AddProjectTaskDependencyCommand(Guid ProjectId, Guid PredecessorId, Guid SuccessorId) : ICommand;

public sealed class AddProjectTaskDependencyCommandValidator : CustomValidator<AddProjectTaskDependencyCommand>
{
    public AddProjectTaskDependencyCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.PredecessorId)
            .NotEmpty();

        RuleFor(x => x.SuccessorId)
            .NotEmpty();

        RuleFor(x => x)
            .Must(x => x.PredecessorId != x.SuccessorId)
            .WithMessage("A task cannot depend on itself.");
    }
}

public sealed class AddProjectTaskDependencyCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<AddProjectTaskDependencyCommandHandler> logger)
    : ICommandHandler<AddProjectTaskDependencyCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<AddProjectTaskDependencyCommandHandler> _logger = logger;

    public async Task<Result> Handle(AddProjectTaskDependencyCommand request, CancellationToken cancellationToken)
    {
        // Load both tasks
        var predecessor = await _ppmDbContext.ProjectTasks
            .Include(t => t.Successors)
            .FirstOrDefaultAsync(t => t.Id == request.PredecessorId && t.ProjectId == request.ProjectId, cancellationToken);
        if (predecessor is null)
        {
            _logger.LogError("Predecessor task {TaskId} not found", request.PredecessorId);
            return Result.Failure("Predecessor task not found");
        }

        var successor = await _ppmDbContext.ProjectTasks
            .FirstOrDefaultAsync(t => t.Id == request.SuccessorId && t.ProjectId == request.ProjectId, cancellationToken);
        if (successor is null)
        {
            _logger.LogError("Successor task {TaskId} not found", request.SuccessorId);
            return Result.Failure("Successor task not found");
        }

        var result = predecessor.AddDependency(successor);
        if (result.IsFailure)
        {
            _logger.LogError("Failed to add dependency from {PredecessorKey} to {SuccessorKey}. Error: {Error}",
                predecessor.Key.Value, successor.Key.Value, result.Error);
            return result;
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully added dependency from {PredecessorKey} to {SuccessorKey}.",
            predecessor.Key.Value, successor.Key.Value);

        return Result.Success();
    }
}
