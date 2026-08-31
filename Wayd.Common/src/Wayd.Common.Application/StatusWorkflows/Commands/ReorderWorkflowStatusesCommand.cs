using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <param name="OrderedStatusIds">
/// Every status of the workflow, in the order wanted. The aggregate refuses a partial list, so a
/// caller moving one status still sends them all.
/// </param>
public sealed record ReorderWorkflowStatusesCommand(
    Guid WorkflowId,
    List<Guid> OrderedStatusIds) : ICommand;

public sealed class ReorderWorkflowStatusesCommandValidator : AbstractValidator<ReorderWorkflowStatusesCommand>
{
    public ReorderWorkflowStatusesCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.OrderedStatusIds).NotEmpty();
    }
}

public sealed class ReorderWorkflowStatusesCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<ReorderWorkflowStatusesCommandHandler> logger)
    : ICommandHandler<ReorderWorkflowStatusesCommand>
{
    private const string AppRequestName = nameof(ReorderWorkflowStatusesCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<ReorderWorkflowStatusesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderWorkflowStatusesCommand request, CancellationToken cancellationToken)
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

            var result = workflow.ReorderStatuses(request.OrderedStatusIds);
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
