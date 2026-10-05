using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Query to get work item metrics for a single sprint.
/// </summary>
public sealed record GetSprintWorkItemMetricsQuery : IQuery<SprintWorkItemMetricsDto?>
{
    public GetSprintWorkItemMetricsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintWorkItemMetricsQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ILogger<GetSprintWorkItemMetricsQueryHandler> logger)
    : IQueryHandler<GetSprintWorkItemMetricsQuery, SprintWorkItemMetricsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ILogger<GetSprintWorkItemMetricsQueryHandler> _logger = logger;

    public async Task<SprintWorkItemMetricsDto?> Handle(
        GetSprintWorkItemMetricsQuery request,
        CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .Select(i => new { i.Id, i.TeamId, i.DateRange.Start })
            .FirstOrDefaultAsync(cancellationToken);

        if (sprint is null)
            return null;

        var schedules = await _dispatcher.LoadSprintSchedules(
            _schedulingSettings,
            [(sprint.Id, sprint.TeamId, sprint.Start)],
            cancellationToken);

        var workItems = await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprint.Id)
            .ToListAsync(cancellationToken);

        return SprintWorkItemMetricsDto.FromWorkItems(sprint.Id, schedules[sprint.Id].SizingMethod, workItems);
    }
}
