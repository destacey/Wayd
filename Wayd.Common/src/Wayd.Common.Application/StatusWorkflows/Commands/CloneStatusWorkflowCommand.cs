using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Copies a workflow into an editable draft.
/// </summary>
/// <remarks>
/// The route by which a published or platform-seeded workflow is changed, since neither can be edited
/// in place: clone, edit the draft, publish it, then reassign the scopes that should use it.
/// </remarks>
public sealed record CloneStatusWorkflowCommand(Guid Id, string Name, string? Description) : ICommand<Guid>;

public sealed class CloneStatusWorkflowCommandValidator : AbstractValidator<CloneStatusWorkflowCommand>
{
    public CloneStatusWorkflowCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}

public sealed class CloneStatusWorkflowCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ILogger<CloneStatusWorkflowCommandHandler> logger)
    : ICommandHandler<CloneStatusWorkflowCommand, Guid>
{
    private const string AppRequestName = nameof(CloneStatusWorkflowCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ILogger<CloneStatusWorkflowCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CloneStatusWorkflowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Statuses must be included or the clone comes back empty — copying them is the point.
            var workflow = await _dbContext.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

            if (workflow is null)
            {
                _logger.LogInformation("Status Workflow {WorkflowId} not found.", request.Id);
                return Result.Failure<Guid>("Status workflow not found.");
            }

            var clone = workflow.Clone(request.Name, request.Description);

            _dbContext.StatusWorkflows.Add(clone);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Status Workflow {WorkflowId} cloned to {CloneId}.", request.Id, clone.Id);

            return Result.Success(clone.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
