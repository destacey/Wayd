using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>Switches a draft workflow between allowing any move and allowing only its transitions.</summary>
public sealed record SetWorkflowTransitionModeCommand(Guid WorkflowId, TransitionMode Mode) : ICommand;

/// <summary>Requires a workflow and a known mode.</summary>
public sealed class SetWorkflowTransitionModeCommandValidator : AbstractValidator<SetWorkflowTransitionModeCommand>
{
    public SetWorkflowTransitionModeCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
    }
}

/// <summary>Loads the workflow and applies the mode through the aggregate, which enforces Draft-only.</summary>
public sealed class SetWorkflowTransitionModeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<SetWorkflowTransitionModeCommandHandler> logger)
    : ICommandHandler<SetWorkflowTransitionModeCommand>
{
    private const string AppRequestName = nameof(SetWorkflowTransitionModeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SetWorkflowTransitionModeCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetWorkflowTransitionModeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _dbContext.StatusWorkflows
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.SetTransitionMode(request.Mode, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to change the transition mode of Status Workflow {WorkflowId}. Error message: {Error}",
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
