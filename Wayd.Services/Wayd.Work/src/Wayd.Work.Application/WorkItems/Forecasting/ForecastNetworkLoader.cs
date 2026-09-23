using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Application.WorkItems.Forecasting;

internal sealed class ForecastNetworkLoader(IWorkDbContext workDbContext)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>
    /// Loads the targets and, unless <paramref name="followDependencies"/> is false, follows
    /// active dependencies back from each open item to its predecessors. Done and removed
    /// predecessors are left out with their dependencies: neither holds anything up, since
    /// removed work will never be finished.
    /// </summary>
    public async Task<ForecastNetwork> Load(IReadOnlyCollection<Guid> targetIds, bool followDependencies, CancellationToken cancellationToken)
    {
        Guard.Against.Null(targetIds);

        var items = new Dictionary<Guid, ForecastNetworkItem>();
        var dependencies = new HashSet<ForecastDependency<Guid>>();
        var frontier = targetIds.Distinct().ToList();

        while (frontier.Count > 0)
        {
            var loaded = await _workDbContext.WorkItems
                .Where(w => frontier.Contains(w.Id))
                .Select(w => new ForecastNetworkItem(
                    w.Id,
                    w.Key,
                    w.Title,
                    w.TeamId,
                    w.StatusCategory,
                    w.Type.Level != null ? w.Type.Level.Tier : (WorkTypeTier?)null,
                    w.StackRank))
                .ToListAsync(cancellationToken);

            foreach (var item in loaded)
                items[item.Id] = item;

            var openIds = loaded.Where(i => i.IsOpen).Select(i => i.Id).ToList();
            if (!followDependencies || openIds.Count == 0)
                break;

            var links = await _workDbContext.WorkItemDependencies
                .Where(d => d.RemovedOn == null && openIds.Contains(d.TargetId))
                .Select(d => new { d.SourceId, d.TargetId })
                .ToListAsync(cancellationToken);

            var next = new HashSet<Guid>();
            foreach (var link in links)
            {
                dependencies.Add(new ForecastDependency<Guid>(link.SourceId, link.TargetId));
                if (!items.ContainsKey(link.SourceId))
                    next.Add(link.SourceId);
            }
            frontier = [.. next];
        }

        var targets = targetIds.ToHashSet();
        var closed = items.Values.Where(i => !i.IsOpen && !targets.Contains(i.Id)).ToDictionary(i => i.Id);
        foreach (var id in closed.Keys)
            items.Remove(id);

        var network = dependencies
            .Where(d => items.ContainsKey(d.Predecessor) && items.ContainsKey(d.Successor))
            .ToList();

        var removedPredecessorDependencies = dependencies
            .Where(d => closed.TryGetValue(d.Predecessor, out var p) && p.StatusCategory == WorkStatusCategory.Removed && items.ContainsKey(d.Successor))
            .Select(d => (closed[d.Predecessor], items[d.Successor]))
            .ToList();

        return new ForecastNetwork(items, network, removedPredecessorDependencies, await BacklogPositions(items.Values, cancellationToken));
    }

    /// <summary>
    /// The open backlog work items beneath any of the given portfolio work items, at any depth.
    /// Walks every root's tree together, one query per level.
    /// </summary>
    public async Task<HashSet<Guid>> OpenBacklogDescendants(IReadOnlyCollection<Guid> portfolioItemIds, CancellationToken cancellationToken)
    {
        var descendants = new HashSet<Guid>();
        var visited = portfolioItemIds.ToHashSet();
        var frontier = visited.ToList();

        while (frontier.Count > 0)
        {
            var children = await _workDbContext.WorkItems
                .Where(w => w.ParentId.HasValue && frontier.Contains(w.ParentId.Value))
                .Select(w => new
                {
                    w.Id,
                    w.StatusCategory,
                    Tier = w.Type.Level != null ? w.Type.Level.Tier : (WorkTypeTier?)null,
                })
                .ToListAsync(cancellationToken);

            frontier = [];
            foreach (var child in children.Where(c => visited.Add(c.Id)))
            {
                if (child.Tier == WorkTypeTier.Portfolio)
                    frontier.Add(child.Id);
                else if (child.Tier == WorkTypeTier.Requirement
                    && child.StatusCategory is WorkStatusCategory.Proposed or WorkStatusCategory.Active)
                    descendants.Add(child.Id);
            }
        }

        return descendants;
    }

    private async Task<Dictionary<Guid, int>> BacklogPositions(IEnumerable<ForecastNetworkItem> items, CancellationToken cancellationToken)
    {
        var placeable = items.Where(i => i.IsOpen && i.IsBacklogItem && i.TeamId.HasValue).ToList();
        if (placeable.Count == 0)
            return [];

        var teamIds = placeable.Select(i => i.TeamId!.Value).Distinct().ToList();
        var backlogs = await OrderedBacklogs(teamIds, cancellationToken);

        var wanted = placeable.Select(i => i.Id).ToHashSet();
        var positions = new Dictionary<Guid, int>();
        foreach (var backlog in backlogs.Values)
        {
            foreach (var (index, id) in backlog.Index())
            {
                if (wanted.Contains(id))
                    positions[id] = index + 1;
            }
        }

        return positions;
    }

    /// <summary>
    /// The ids of a team's open backlog items, first-ranked first.
    /// </summary>
    public async Task<List<Guid>> TeamBacklog(Guid teamId, CancellationToken cancellationToken) =>
        (await OrderedBacklogs([teamId], cancellationToken)).GetValueOrDefault(teamId) ?? [];

    public async Task<Dictionary<Guid, ForecastBacklogEntry>> Entries(IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
            return [];

        return await _workDbContext.WorkItems
            .Where(w => workItemIds.Contains(w.Id))
            .Select(w => new ForecastBacklogEntry(w.Id, w.Key, w.Title))
            .ToDictionaryAsync(e => e.Id, cancellationToken);
    }

    /// <summary>
    /// The same backlogs GetTeamBacklogQuery shows, in the same order. Items the source system
    /// has not ranked share one high rank, so they fall behind every ranked item, oldest first.
    /// </summary>
    /// <remarks>
    /// Ordered here rather than in SQL: SQL Server orders GUIDs differently from .NET, so the
    /// tie-break would disagree with positions computed elsewhere. Only the ordering columns are
    /// read — a team's backlog can run to thousands of items.
    /// </remarks>
    private async Task<Dictionary<Guid, List<Guid>>> OrderedBacklogs(List<Guid> teamIds, CancellationToken cancellationToken)
    {
        var items = await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Proposed || w.StatusCategory == WorkStatusCategory.Active)
            .Select(w => new { w.Id, TeamId = w.TeamId!.Value, w.StackRank, w.Created })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(w => w.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(w => w.StackRank).ThenBy(w => w.Created).ThenBy(w => w.Id)
                    .Select(w => w.Id)
                    .ToList());
    }
}
