using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Query to get combined backlog items for multiple sprints.
/// </summary>
public sealed record GetSprintsBacklogQuery(IEnumerable<Guid> SprintIds) : IQuery<List<SprintBacklogItemDto>>;

public sealed class GetSprintsBacklogQueryHandler(
    IWorkDbContext workDbContext,
    ILogger<GetSprintsBacklogQueryHandler> logger)
    : IQueryHandler<GetSprintsBacklogQuery, List<SprintBacklogItemDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetSprintsBacklogQueryHandler> _logger = logger;

    public async Task<List<SprintBacklogItemDto>> Handle(
        GetSprintsBacklogQuery request,
        CancellationToken cancellationToken)
    {
        var sprintIds = request.SprintIds as Guid[] ?? request.SprintIds.ToArray();

        if (sprintIds.Length == 0)
        {
            return [];
        }

        // Get all work items for the sprints in a single query
        var workItems = await _workDbContext.WorkItems
            .Where(w => w.IterationId.HasValue && sprintIds.Contains(w.IterationId.Value))
            .ProjectToType<SprintBacklogItemDto>()
            .ToListAsync(cancellationToken);

        if (workItems.Count == 0)
        {
            return [];
        }

        // Assign rank grouped by team (each team has their own ranking)
        var orderedBacklog = workItems
            .GroupBy(w => w.Team?.Id)
            .SelectMany(teamGroup =>
                teamGroup
                    .OrderBy(w => w.StackRank)
                    .ThenBy(w => w.Created)
                    .Select((workItem, index) =>
                    {
                        workItem.Rank = index + 1;
                        return workItem;
                    }))
            .OrderBy(w => w.Team?.Name)
            .ThenBy(w => w.Rank)
            .ToList();

        return orderedBacklog;
    }
}
