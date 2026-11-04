using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItems.Commands;

/// <summary>
/// Links Portfolio-tier work items to a project, from any workspace. Work items beneath them that have no
/// project of their own inherit it.
/// </summary>
/// <param name="ProjectId">The project to link the work items to.</param>
/// <param name="WorkItemIds">The work items to link; each must be Portfolio tier.</param>
public sealed record AddProjectWorkItemsCommand(Guid ProjectId, IReadOnlyList<Guid> WorkItemIds) : ICommand;

/// <summary>Checks the project and work item ids, and the size of the request.</summary>
public sealed class AddProjectWorkItemsCommandValidator : AbstractValidator<AddProjectWorkItemsCommand>
{
    /// <summary>The most work items one request may change.</summary>
    public const int MaxWorkItems = 100;

    public AddProjectWorkItemsCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.WorkItemIds)
            .NotEmpty()
            .Must(ids => ids.Count <= MaxWorkItems)
                .WithMessage($"No more than {MaxWorkItems} work items can be linked at once.");

        RuleForEach(x => x.WorkItemIds)
            .NotEmpty();
    }
}

/// <summary>
/// Links the work items in one unit of work: none change unless every one exists and is Portfolio tier.
/// </summary>
public sealed class AddProjectWorkItemsCommandHandler(
    IWorkDbContext workDbContext,
    ILogger<AddProjectWorkItemsCommandHandler> logger) : ICommandHandler<AddProjectWorkItemsCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<AddProjectWorkItemsCommandHandler> _logger = logger;

    /// <summary>Links the work items, then passes the project down to the work items beneath each one that changed.</summary>
    public async Task<Result> Handle(AddProjectWorkItemsCommand request, CancellationToken cancellationToken)
    {
        var projectExists = await _workDbContext.WorkProjects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
            // The Work module's copy of a project arrives by replication, so a brand-new one can lag.
            return Result.Failure("Project not found, or not yet available to Work Management.");

        var ids = request.WorkItemIds.Distinct().ToList();
        var workItems = await _workDbContext.WorkItems
            .Include(w => w.Type)
                .ThenInclude(t => t.Level)
            .Where(w => ids.Contains(w.Id))
            .ToListAsync(cancellationToken);

        if (workItems.Count != ids.Count)
            return Result.Failure("One or more work items were not found.");

        var notPortfolio = workItems
            .Where(w => w.Type.Level!.Tier != WorkTypeTier.Portfolio)
            .Select(w => w.Key.ToString())
            .ToList();
        if (notPortfolio.Count > 0)
            return Result.Failure($"Only portfolio tier work items can be linked to a project: {string.Join(", ", notPortfolio)}.");

        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);

        var changed = new List<WorkItem>();
        foreach (var workItem in workItems)
        {
            var before = workItem.CurrentProjectId;

            var result = workItem.UpdateProjectId(request.ProjectId);
            if (result.IsFailure)
                return result;

            if (workItem.CurrentProjectId != before)
                changed.Add(workItem);
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        foreach (var workItem in changed)
        {
            await WorkItemProjectInheritance.PushToDescendants(_workDbContext, workItem, _logger, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation("Linked {Count} work items to project {ProjectId}.", changed.Count, request.ProjectId);

        return Result.Success();
    }
}
