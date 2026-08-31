using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record PublishStatusWorkflowCommand(Guid Id) : ICommand;

public sealed class PublishStatusWorkflowCommandValidator : AbstractValidator<PublishStatusWorkflowCommand>
{
    public PublishStatusWorkflowCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class PublishStatusWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<PublishStatusWorkflowCommandHandler> logger)
    : ICommandHandler<PublishStatusWorkflowCommand>
{
    private const string AppRequestName = nameof(PublishStatusWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<PublishStatusWorkflowCommandHandler> _logger = logger;

    public async Task<Result> Handle(PublishStatusWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Statuses must be included: Publish checks that every required alias is carried by one of
            // them, and an unloaded collection reads as "no status carries any alias" — so a workflow
            // that genuinely has them would be refused.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.Id);
                return Result.Failure("Status workflow not found.");
            }

            var result = workflow.Publish(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                workflow.ClearDomainEvents();

                _logger.LogInformation("Unable to publish Status Workflow {WorkflowId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Status Workflow {WorkflowId} published.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
