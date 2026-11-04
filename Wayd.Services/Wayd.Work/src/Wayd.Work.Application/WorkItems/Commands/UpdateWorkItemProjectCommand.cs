using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>Sets or clears a Portfolio-tier work item's own project.</summary>
/// <param name="WorkspaceId">The workspace the work item must belong to.</param>
/// <param name="WorkItemId">The work item's id.</param>
/// <param name="ProjectId">The project to assign, or null to inherit the parent's project again.</param>
public sealed record UpdateWorkItemProjectCommand(Guid WorkspaceId, Guid WorkItemId, Guid? ProjectId) : ICommand;

public sealed class UpdateWorkItemProjectCommandValidator : AbstractValidator<UpdateWorkItemProjectCommand>
{
    public UpdateWorkItemProjectCommandValidator()
    {
        RuleFor(x => x.WorkspaceId)
            .NotEmpty();

        RuleFor(x => x.WorkItemId)
            .NotEmpty();

        When(x => x.ProjectId.HasValue, () =>
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty();
        });
    }
}

public sealed class UpdateWorkItemProjectCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<UpdateWorkItemProjectCommandHandler> logger) : ICommandHandler<UpdateWorkItemProjectCommand>
{
    private const string AppRequestName = nameof(UpdateWorkItemProjectCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<UpdateWorkItemProjectCommandHandler> _logger = logger;
    public async Task<Result> Handle(UpdateWorkItemProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await _workDbContext.WorkItems
                .Include(i => i.Type)
                    .ThenInclude(t => t.Level)
                .FirstOrDefaultAsync(w => w.Id == request.WorkItemId && w.WorkspaceId == request.WorkspaceId, cancellationToken);
            if (workItem is null)
            {
                _logger.LogInformation("Work item {WorkItemId} not found in workspace {WorkspaceId}.", request.WorkItemId, request.WorkspaceId);
                return Result.Failure("Work item not found.");
            }

            var originalProjectId = workItem.ProjectId;

            var result = workItem.UpdateProjectId(request.ProjectId);
            if (result.IsFailure)
            {
                // Reset the entity
                await _workDbContext.Entry(workItem).ReloadAsync(cancellationToken);
                workItem.ClearDomainEvents();

                _logger.LogError("Unable to update work item {WorkItemId} project id to {ProjectId}. Error message: {Error}", request.WorkItemId, request.ProjectId?.ToString() ?? "null", result.Error);
                return Result.Failure(result.Error);
            }

            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Work item {WorkItemId} project id updated.", request.WorkItemId);

            if (originalProjectId != workItem.ProjectId)
            {
                await WorkItemProjectInheritance.PushToDescendants(_workDbContext, workItem, _logger, cancellationToken);

                _logger.LogInformation("Work item {WorkItemId} descendants updated.", request.WorkItemId);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}