using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record RenameWorkflowStatusCommand(
    Guid WorkflowId,
    Guid StatusId,
    string Name,
    string? Description) : ICommand;

public sealed class RenameWorkflowStatusCommandValidator : AbstractValidator<RenameWorkflowStatusCommand>
{
    public RenameWorkflowStatusCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.StatusId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(512);
    }
}

public sealed class RenameWorkflowStatusCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<RenameWorkflowStatusCommandHandler> logger)
    : ICommandHandler<RenameWorkflowStatusCommand>
{
    private const string AppRequestName = nameof(RenameWorkflowStatusCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<RenameWorkflowStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameWorkflowStatusCommand request, CancellationToken cancellationToken)
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

            var result = workflow.RenameStatus(request.StatusId, request.Name, request.Description);
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
