using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Query to get work item metrics for multiple sprints.
/// Used for PI Iteration metrics aggregation.
/// </summary>
public sealed record GetSprintsWorkItemMetricsQuery(IEnumerable<Guid> SprintIds) : IQuery<List<SprintWorkItemMetricsDto>>;

public sealed class GetSprintsWorkItemMetricsQueryHandler(
    IWorkDbContext workDbContext,
    ILogger<GetSprintsWorkItemMetricsQueryHandler> logger)
    : IQueryHandler<GetSprintsWorkItemMetricsQuery, List<SprintWorkItemMetricsDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetSprintsWorkItemMetricsQueryHandler> _logger = logger;

    public async Task<List<SprintWorkItemMetricsDto>> Handle(
        GetSprintsWorkItemMetricsQuery request,
        CancellationToken cancellationToken)
    {
        var sprintIdsList = request.SprintIds.ToList();

        if (sprintIdsList.Count == 0)
        {
            return [];
        }

        // Get all work items for the sprints in a single query
        var workItemsBySprintId = await _workDbContext.WorkItems
            .Where(w => w.IterationId.HasValue && sprintIdsList.Contains(w.IterationId.Value))
            .GroupBy(w => w.IterationId!.Value)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.ToList(),
                cancellationToken);

        // Calculate metrics for each sprint (including sprints with no work items)
        var result = sprintIdsList
            .Select(sprintId =>
            {
                var workItems = workItemsBySprintId.TryGetValue(sprintId, out var items)
                    ? items
                    : [];
                return SprintWorkItemMetricsDto.FromWorkItems(sprintId, workItems);
            })
            .ToList();

        return result;
    }
}
