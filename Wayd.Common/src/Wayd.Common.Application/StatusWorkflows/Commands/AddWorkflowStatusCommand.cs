using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record AddWorkflowStatusCommand(
    Guid WorkflowId,
    string Name,
    string? Description,
    StatusCategory Category,
    int Alias) : ICommand<Guid>;

public sealed class AddWorkflowStatusCommandValidator : AbstractValidator<AddWorkflowStatusCommand>
{
    public AddWorkflowStatusCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class AddWorkflowStatusCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<AddWorkflowStatusCommandHandler> logger)
    : ICommandHandler<AddWorkflowStatusCommand, Guid>
{
    private const string AppRequestName = nameof(AddWorkflowStatusCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<AddWorkflowStatusCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddWorkflowStatusCommand request, CancellationToken cancellationToken)
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
                return Result.Failure<Guid>("Status workflow not found.");
            }

            var result = workflow.AddStatus(request.Name, request.Description, request.Category, request.Alias);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Unable to add a status to Status Workflow {WorkflowId}. Error message: {Error}",
                    request.WorkflowId, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(result.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
