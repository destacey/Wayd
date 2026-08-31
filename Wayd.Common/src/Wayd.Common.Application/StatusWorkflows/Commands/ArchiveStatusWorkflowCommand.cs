using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record ArchiveStatusWorkflowCommand(Guid Id) : ICommand;

public sealed class ArchiveStatusWorkflowCommandValidator : AbstractValidator<ArchiveStatusWorkflowCommand>
{
    public ArchiveStatusWorkflowCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ArchiveStatusWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ArchiveStatusWorkflowCommandHandler> logger)
    : ICommandHandler<ArchiveStatusWorkflowCommand>
{
    private const string AppRequestName = nameof(ArchiveStatusWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ArchiveStatusWorkflowCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveStatusWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _dbContext.StatusWorkflows
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.Id);
                return Result.Failure("Status workflow not found.");
            }

            // Whether anything still uses it is the handler's to answer: the aggregate cannot see the
            // assignments table, so it takes the answer as an argument.
            var isAssigned = await _dbContext.WorkflowAssignments
                .AnyAsync(a => a.WorkflowId == request.Id, cancellationToken);

            var result = workflow.Archive(isAssigned, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (result.IsFailure)
            {
                workflow.ClearDomainEvents();

                _logger.LogInformation("Unable to archive Status Workflow {WorkflowId}. Error message: {Error}", request.Id, result.Error);
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
