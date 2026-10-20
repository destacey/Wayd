using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>A move to allow, from one status of the workflow to another, with what it needs.</summary>
/// <param name="Conditions">The conditions the move carries; <c>null</c> or empty for none.</param>
public sealed record WorkflowTransitionPair(
    Guid FromStatusId,
    Guid ToStatusId,
    IReadOnlyCollection<TransitionConditionKind>? Conditions = null);

/// <summary>
/// Replaces the moves a draft workflow allows, and their conditions, with exactly <see cref="Transitions"/>.
/// </summary>
public sealed record SetWorkflowTransitionsCommand(Guid WorkflowId, IReadOnlyCollection<WorkflowTransitionPair> Transitions) : ICommand;

/// <summary>Requires a workflow and both ends of every transition.</summary>
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
            t.RuleForEach(p => p.Conditions).IsInEnum();
        });
    }
}

/// <summary>Loads the workflow with its statuses and transitions and replaces the set through the aggregate.</summary>
public sealed class SetWorkflowTransitionsCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ITransitionConditionChecker conditionChecker,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetWorkflowTransitionsCommandHandler> logger)
    : ICommandHandler<SetWorkflowTransitionsCommand>
{
    private const string AppRequestName = nameof(SetWorkflowTransitionsCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetWorkflowTransitionsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetWorkflowTransitionsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Both collections must be loaded: the statuses to check each transition's ends against, and
            // the transitions so the ones kept retain their identity rather than being re-added. Their
            // conditions likewise, or every kept condition would read as new and be inserted twice.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .Include(w => w.Transitions)
                    .ThenInclude(t => t.Conditions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            // Only an editable draft is asked, so a workflow that cannot change is refused for that
            // reason rather than for a condition it could never have taken anyway.
            var unavailable = workflow.IsSystem || workflow.State != StatusWorkflowState.Draft
                ? null
                : UnavailableAddition(workflow, request.Transitions);
            if (unavailable is { } kind)
            {
                _logger.LogInformation(
                    "Unable to change the transitions of Status Workflow {WorkflowId}: nothing evaluates {Condition}.",
                    request.WorkflowId, kind);
                return Result.Failure($"A transition cannot require {kind.DescribeMissing()} yet: nothing can check it.");
            }

            var result = workflow.SetTransitions(
                [.. request.Transitions.Select(t => new WorkflowTransitionDefinition(t.FromStatusId, t.ToStatusId, t.Conditions))],
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

    /// <summary>
    /// The first condition the request adds that nothing can check, or <c>null</c>. Only additions are
    /// refused, so a condition a move already holds never blocks editing the rest of the set.
    /// </summary>
    private TransitionConditionKind? UnavailableAddition(StatusWorkflow workflow, IEnumerable<WorkflowTransitionPair> requested)
    {
        foreach (var pair in requested)
        {
            var held = workflow.Transitions
                .SingleOrDefault(t => t.FromStatusId == pair.FromStatusId && t.ToStatusId == pair.ToStatusId)?
                .ConditionKinds() ?? [];

            foreach (var kind in pair.Conditions ?? [])
            {
                if (!held.Contains(kind) && Enum.IsDefined(kind) && !_conditionChecker.IsAvailable(kind))
                {
                    return kind;
                }
            }
        }

        return null;
    }
}
