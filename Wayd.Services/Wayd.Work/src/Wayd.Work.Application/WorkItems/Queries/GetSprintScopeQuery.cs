using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Gets what a sprint committed to and what became of it, from work item history. Null when there is no such
/// sprint, or it has no planned dates to measure between.
/// </summary>
public sealed record GetSprintScopeQuery : IQuery<SprintScopeDto?>
{
    public GetSprintScopeQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintScopeQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetSprintScopeQuery, SprintScopeDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<SprintScopeDto?> Handle(GetSprintScopeQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null || sprint.DateRange.Start is null || sprint.DateRange.End is null)
            return null;

        var (window, sizingMethod) = await LoadWindow(sprint, cancellationToken);

        var report = SprintScopeReport.Build(window, sizingMethod, await LoadPeriods(window, cancellationToken));

        var workItemIds = report.Items.Select(i => i.WorkItemId).ToList();
        var workItems = await _workDbContext.WorkItems
            .Where(w => workItemIds.Contains(w.Id))
            .ProjectToType<SprintBacklogItemDto>()
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        return new SprintScopeDto
        {
            SprintId = sprint.Id,
            SizingMethod = sizingMethod,
            EffectiveStart = window.Start,
            StartIsActual = window.StartIsActual,
            EffectiveEnd = window.End,
            EndIsActual = window.EndIsActual,
            LastDay = window.LastDay,
            TimeZone = window.TimeZone.Id,
            HasTeam = sprint.TeamId is not null,
            HistoryIncomplete = await IsHistoryIncomplete(sprint.Id, workItemIds, cancellationToken),
            Totals = SprintScopeTotalsDto.From(report.Totals),
            Items = [.. report.Items
                .Where(i => workItems.ContainsKey(i.WorkItemId))
                .Select(i => new SprintScopeItemDto
                {
                    WorkItem = workItems[i.WorkItemId],
                    Entry = i.Entry,
                    Outcome = i.Outcome,
                    EnteredAt = i.EnteredAt,
                    LeftAt = i.LeftAt,
                    EntryEstimate = i.EntryEstimate,
                    OutcomeEstimate = i.OutcomeEstimate,
                })
                .OrderBy(i => i.WorkItem.StackRank)
                .ThenBy(i => i.WorkItem.Created)],
        };
    }

    private async Task<(SprintScopeWindow Window, SizingMethod SizingMethod)> LoadWindow(Iteration sprint, CancellationToken cancellationToken)
    {
        if (sprint.TeamId is { } teamId)
        {
            var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, teamId, tracked: false, cancellationToken);
            var onTimeline = timeline.Sprints.Single(s => s.Id == sprint.Id);
            return (SprintScopeWindow.For(timeline, onTimeline), timeline.ScheduleFor(onTimeline).SizingMethod);
        }

        var schedules = await _dispatcher.LoadSprintSchedules(
            _schedulingSettings,
            [(sprint.Id, sprint.TeamId, sprint.DateRange.Start)],
            cancellationToken);
        var schedule = schedules[sprint.Id];
        return (SprintScopeWindow.Unscheduled(sprint, schedule), schedule.SizingMethod);
    }

    /// <summary>
    /// The history, from the commitment point to the effective end, of every item that was in the sprint
    /// between them. The period an item left into is included, since where it went decides its outcome.
    /// </summary>
    private async Task<List<SprintScopePeriod>> LoadPeriods(SprintScopeWindow window, CancellationToken cancellationToken)
    {
        var sprintId = window.SprintId;
        var start = window.Start;
        var end = window.End;

        var workItemIds = _workDbContext.WorkItemStateHistory
            .Where(h => h.IterationId == sprintId && h.ValidFrom < end && (h.ValidTo == null || h.ValidTo > start))
            .Select(h => h.WorkItemId)
            .Distinct();

        return await _workDbContext.WorkItemStateHistory
            .Where(h => workItemIds.Contains(h.WorkItemId) && h.ValidFrom <= end && (h.ValidTo == null || h.ValidTo > start))
            .Select(h => new SprintScopePeriod(
                h.WorkItemId,
                h.ValidFrom,
                h.ValidTo,
                h.IterationId,
                _workDbContext.WorkTypes.Any(t => t.Id == h.WorkTypeId && t.Level!.Tier == WorkTypeTier.Requirement),
                h.StatusCategory,
                h.StoryPoints,
                h.Effort,
                h.Size))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Whether any workspace holding the sprint's work — the items that were in it, or are in it now — has not
    /// had its history read through to the end. Items in a workspace never read have no history, so they are
    /// found by their current sprint.
    /// </summary>
    private async Task<bool> IsHistoryIncomplete(Guid sprintId, List<Guid> workItemIds, CancellationToken cancellationToken) =>
        await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprintId || workItemIds.Contains(w.Id))
            .AnyAsync(w => w.Workspace.WorkItemHistoryBackfilledOn == null, cancellationToken);
}
