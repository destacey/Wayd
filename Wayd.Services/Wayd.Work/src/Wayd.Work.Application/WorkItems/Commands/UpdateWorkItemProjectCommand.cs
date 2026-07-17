using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Commands;

public sealed record UpdateWorkItemProjectCommand(Guid WorkItemId, Guid? ProjectId) : ICommand;

public sealed class UpdateWorkItemProjectCommandValidator : AbstractValidator<UpdateWorkItemProjectCommand>
{
    public UpdateWorkItemProjectCommandValidator()
    {
        RuleFor(x => x.WorkItemId)
            .NotNull();

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
                .FirstOrDefaultAsync(w => w.Id == request.WorkItemId, cancellationToken);
            if (workItem is null)
            {
                _logger.LogInformation("Work item {WorkItemId} not found.", request.WorkItemId);
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
                await UpdateChildren(workItem.Adapt<WorkItemParentInfo>(), cancellationToken);

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

    // TODO: this should be moved to an event handler
    private async Task UpdateChildren(WorkItemParentInfo parent, CancellationToken cancellationToken)
    {
        if (parent.Tier != WorkTypeTier.Portfolio)
        {
            return;
        }

        var children = await _workDbContext.WorkItems
            .Include(wi => wi.Type)
                .ThenInclude(t => t.Level)
            .Where(wi => wi.ParentId == parent.Id)
            .ToListAsync(cancellationToken);

        if (children.Count == 0)
            return;


        int updateCount = 0;

        foreach (var child in children)
        {
            var result = child.UpdateParent(parent, child.Type);
            if (result.IsFailure)
            {
                _logger.LogError("Error updating parent for work item {WorkItemId}. {Error}", child.Id, result.Error);
                continue;
            }

            updateCount++;
        }

        if (updateCount > 0)
        {
            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Updated {UpdateCount} children for work item {WorkItemId}.", updateCount, parent.Id);
        }

        foreach (var child in children)
        {
            if (child.ProjectId is null)
                await UpdateChildren(child.Adapt<WorkItemParentInfo>(), cancellationToken);
        }
    }
}