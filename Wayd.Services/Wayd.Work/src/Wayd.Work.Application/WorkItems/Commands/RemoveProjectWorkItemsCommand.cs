using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Unlinks work items that are linked directly to a project. Each then inherits its parent's project, if
/// any, and the work items beneath it that have no project of their own follow.
/// </summary>
/// <param name="ProjectId">The project to unlink the work items from.</param>
/// <param name="WorkItemIds">The work items to unlink; each must be linked directly to the project.</param>
public sealed record RemoveProjectWorkItemsCommand(Guid ProjectId, IReadOnlyList<Guid> WorkItemIds) : ICommand;

/// <summary>Checks the project and work item ids, and the size of the request.</summary>
public sealed class RemoveProjectWorkItemsCommandValidator : AbstractValidator<RemoveProjectWorkItemsCommand>
{
    public RemoveProjectWorkItemsCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.WorkItemIds)
            .NotEmpty()
            .Must(ids => ids.Count <= AddProjectWorkItemsCommandValidator.MaxWorkItems)
                .WithMessage($"No more than {AddProjectWorkItemsCommandValidator.MaxWorkItems} work items can be unlinked at once.");

        RuleForEach(x => x.WorkItemIds)
            .NotEmpty();
    }
}

/// <summary>
/// Unlinks the work items in one unit of work: none change unless every one exists and is linked directly.
/// </summary>
public sealed class RemoveProjectWorkItemsCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<RemoveProjectWorkItemsCommandHandler> logger) : ICommandHandler<RemoveProjectWorkItemsCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<RemoveProjectWorkItemsCommandHandler> _logger = logger;

    /// <summary>Unlinks the work items, then passes each one's new project down to the work items beneath it.</summary>
    public async Task<Result> Handle(RemoveProjectWorkItemsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.WorkItemIds.Distinct().ToList();
        var workItems = await _workDbContext.WorkItems
            .Include(w => w.Type)
                .ThenInclude(t => t.Level)
            .Where(w => ids.Contains(w.Id))
            .ToListAsync(cancellationToken);

        if (workItems.Count != ids.Count)
            return Result.Failure("One or more work items were not found.");

        // An inherited link belongs to the parent; unlinking the child alone would leave it on the project.
        var notDirect = workItems
            .Where(w => w.ProjectId != request.ProjectId)
            .Select(w => w.Key.ToString())
            .ToList();
        if (notDirect.Count > 0)
            return Result.Failure($"Only work items linked directly to the project can be removed: {string.Join(", ", notDirect)}. A work item that inherits the project leaves it with its parent.");

        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        foreach (var workItem in workItems)
        {
            var result = workItem.UpdateProjectId(null);
            if (result.IsFailure)
                return result;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        foreach (var workItem in workItems)
        {
            await WorkItemProjectInheritance.PushToDescendants(_workDbContext, workItem, _logger, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Unlinked {Count} work items from project {ProjectId}.", workItems.Count, request.ProjectId);

        return Result.Success();
    }
}
