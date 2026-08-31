using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

public sealed record CreateStatusWorkflowCommand(string Name, string? Description, string OwnerType)
    : ICommand<Guid>;

public sealed class CreateStatusWorkflowCommandValidator : AbstractValidator<CreateStatusWorkflowCommand>
{
    public CreateStatusWorkflowCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.OwnerType).NotEmpty();
    }
}

public sealed class CreateStatusWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<CreateStatusWorkflowCommandHandler> logger)
    : ICommandHandler<CreateStatusWorkflowCommand, Guid>
{
    private const string AppRequestName = nameof(CreateStatusWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<CreateStatusWorkflowCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateStatusWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = StatusWorkflow.Create(request.Name, request.Description, request.OwnerType);
            if (result.IsFailure)
            {
                _logger.LogInformation("Unable to create workflow. Error message: {Error}", result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            _dbContext.StatusWorkflows.Add(result.Value);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Status Workflow {WorkflowId} created.", result.Value.Id);

            return Result.Success(result.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
