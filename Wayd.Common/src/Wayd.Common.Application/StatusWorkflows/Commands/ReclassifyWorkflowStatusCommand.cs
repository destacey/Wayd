using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Changes a status category and its well-known meaning.
/// </summary>
/// <remarks>
/// Separate from a rename because it is bounded differently: a name is cosmetic, but records carry a
/// denormalized category and alias, so these can only move while the workflow is still a draft.
/// </remarks>
public sealed record ReclassifyWorkflowStatusCommand(
    Guid WorkflowId,
    Guid StatusId,
    StatusCategory Category,
    int Alias) : ICommand;

public sealed class ReclassifyWorkflowStatusCommandValidator : AbstractValidator<ReclassifyWorkflowStatusCommand>
{
    public ReclassifyWorkflowStatusCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.StatusId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class ReclassifyWorkflowStatusCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ReclassifyWorkflowStatusCommandHandler> logger)
    : ICommandHandler<ReclassifyWorkflowStatusCommand>
{
    private const string AppRequestName = nameof(ReclassifyWorkflowStatusCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ReclassifyWorkflowStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReclassifyWorkflowStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Every status mutation runs through the aggregate, so its collection must be loaded: it
            // enforces name and alias uniqueness across siblings that a lone status cannot see.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.ReclassifyStatus(
                request.StatusId,
                request.Category,
                request.Alias,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);
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
