using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>A move to allow, from one status of the workflow to another.</summary>
public sealed record WorkflowTransitionPair(Guid FromStatusId, Guid ToStatusId);

/// <summary>Replaces the moves a draft workflow allows with exactly <see cref="Transitions"/>.</summary>
public sealed record SetWorkflowTransitionsCommand(Guid WorkflowId, IReadOnlyCollection<WorkflowTransitionPair> Transitions) : ICommand;

public sealed class SetWorkflowTransitionsCommandValidator : AbstractValidator<SetWorkflowTransitionsCommand>
{
    public SetWorkflowTransitionsCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.Transitions).NotNull();
        RuleForEach(x => x.Transitions).ChildRules(t =>
        {
            t.RuleFor(p => p.FromStatusId).NotEmpty();
            t.RuleFor(p => p.ToStatusId).NotEmpty();
        });
    }
}

public sealed class SetWorkflowTransitionsCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetWorkflowTransitionsCommandHandler> logger)
    : ICommandHandler<SetWorkflowTransitionsCommand>
{
    private const string AppRequestName = nameof(SetWorkflowTransitionsCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetWorkflowTransitionsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetWorkflowTransitionsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Both collections must be loaded: the statuses to check each transition's ends against, and
            // the transitions so the ones kept retain their identity rather than being re-added.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .Include(w => w.Transitions)
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.SetTransitions(
                [.. request.Transitions.Select(t => (t.FromStatusId, t.ToStatusId))],
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change the transitions of Status Workflow {WorkflowId}. Error message: {Error}",
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
