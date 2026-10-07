using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
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
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ILogger<GetSprintsWorkItemMetricsQueryHandler> logger)
    : IQueryHandler<GetSprintsWorkItemMetricsQuery, List<SprintWorkItemMetricsDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
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

        var sprints = await _workDbContext.Iterations
            .Where(i => sprintIdsList.Contains(i.Id))
            .Select(i => new { i.Id, i.TeamId, i.DateRange.Start })
            .ToListAsync(cancellationToken);

        var schedules = await _dispatcher.LoadSprintSchedules(
            _schedulingSettings,
            [.. sprints.Select(s => (s.Id, s.TeamId, s.Start))],
            cancellationToken);

        // Requirement-tier work only, as sprint scope counts it, so the two agree.
        var workItemsBySprintId = await _workDbContext.WorkItems
            .Where(w => w.IterationId.HasValue && sprintIdsList.Contains(w.IterationId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
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
                var sizingMethod = schedules.TryGetValue(sprintId, out var schedule) ? schedule.SizingMethod : SizingMethod.Count;
                return SprintWorkItemMetricsDto.FromWorkItems(sprintId, sizingMethod, workItems);
            })
            .ToList();

        return result;
    }
}
