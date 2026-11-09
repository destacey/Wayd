using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Writes, changes or clears a status's description and nothing else. The only status change an
/// administrator can make to a managed workflow, whose names belong to its source.
/// </summary>
public sealed record DescribeWorkflowStatusCommand(
    Guid WorkflowId,
    Guid StatusId,
    string? Description) : ICommand;

/// <summary>Caps the description at the length a status stores.</summary>
public sealed class DescribeWorkflowStatusCommandValidator : AbstractValidator<DescribeWorkflowStatusCommand>
{
    public DescribeWorkflowStatusCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.StatusId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(512);
    }
}

/// <summary>
/// Describes the status through its workflow, which refuses system and archived workflows and records the
/// change, attributed to the current user, in the Activity log.
/// </summary>
public sealed class DescribeWorkflowStatusCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<DescribeWorkflowStatusCommandHandler> logger)
    : ICommandHandler<DescribeWorkflowStatusCommand>
{
    private const string AppRequestName = nameof(DescribeWorkflowStatusCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DescribeWorkflowStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(DescribeWorkflowStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.WorkflowId);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.DescribeStatus(request.StatusId, request.Description, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                workflow.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to describe status {StatusId} on Status Workflow {WorkflowId}. Error message: {Error}",
                    request.StatusId, request.WorkflowId, result.Error);
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
