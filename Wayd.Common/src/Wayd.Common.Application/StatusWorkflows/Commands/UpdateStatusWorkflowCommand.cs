using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record UpdateStatusWorkflowCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateStatusWorkflowCommandValidator : AbstractValidator<UpdateStatusWorkflowCommand>
{
    public UpdateStatusWorkflowCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}

public sealed class UpdateStatusWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<UpdateStatusWorkflowCommandHandler> logger)
    : ICommandHandler<UpdateStatusWorkflowCommand>
{
    private const string AppRequestName = nameof(UpdateStatusWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<UpdateStatusWorkflowCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateStatusWorkflowCommand request, CancellationToken cancellationToken)
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

            var result = workflow.Update(request.Name, request.Description);
            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to update Status Workflow {WorkflowId}. Error message: {Error}", request.Id, result.Error);
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
