using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record RemoveWorkflowStatusCommand(Guid WorkflowId, Guid StatusId) : ICommand;

public sealed class RemoveWorkflowStatusCommandValidator : AbstractValidator<RemoveWorkflowStatusCommand>
{
    public RemoveWorkflowStatusCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.StatusId).NotEmpty();
    }
}

public sealed class RemoveWorkflowStatusCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RemoveWorkflowStatusCommandHandler> logger)
    : ICommandHandler<RemoveWorkflowStatusCommand>
{
    private const string AppRequestName = nameof(RemoveWorkflowStatusCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RemoveWorkflowStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveWorkflowStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Every status mutation runs through the aggregate, so its collection must be loaded: it
            // enforces name and alias uniqueness across siblings that a lone status cannot see.
            // Transitions too: removing a status removes the transitions touching it, and the database
            // refuses to delete a status a transition still references.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .Include(w => w.Transitions)
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.RemoveStatus(request.StatusId, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                workflow.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to change statuses on Status Workflow {WorkflowId}. Error message: {Error}",
                    request.WorkflowId, result.Error);
                return Result.Failure(result.Error);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
