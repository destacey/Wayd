using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// Measures the Kanban teams among <paramref name="TeamIds"/> over the dates from <paramref name="Start"/> to
/// <paramref name="End"/>, both included. A team is Kanban when its operating model on <paramref name="Start"/>
/// says so, or on <paramref name="End"/> for a team whose models begin inside the span. Every other id — a Scrum
/// team, a team of teams, a team with no operating model then — is left out of the result. The team's cadence
/// plays no part.
/// </summary>
/// <remarks>
/// Work is attributed to the team an item is on now, so an item that moved teams counts for its current team in
/// past windows.
/// </remarks>
public sealed record GetKanbanTeamsWindowMetricsQuery(IReadOnlyCollection<Guid> TeamIds, LocalDate Start, LocalDate End) : IQuery<List<KanbanTeamWindowMetricsDto>>;

/// <summary>
/// Reads the teams' operating models from Organization, then their completed work from done dates and their
/// in-progress work from the items now, for the current window, or from work item history at a past window's end.
/// </summary>
public sealed class GetKanbanTeamsWindowMetricsQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetKanbanTeamsWindowMetricsQuery, List<KanbanTeamWindowMetricsDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc />
    public async Task<List<KanbanTeamWindowMetricsDto>> Handle(GetKanbanTeamsWindowMetricsQuery request, CancellationToken cancellationToken)
    {
        if (request.TeamIds.Count == 0)
            return [];

        var teams = await _workDbContext.WorkTeams
            .Where(t => request.TeamIds.Contains(t.Id) && t.Type == TeamType.Team)
            .Select(t => new NavigationDto { Id = t.Id, Key = t.Key, Name = t.Name })
            .ToListAsync(cancellationToken);
        if (teams.Count == 0)
            return [];

        var schedules = await _dispatcher.Send(new GetTeamsScheduleHistoryQuery([.. teams.Select(t => t.Id)]), cancellationToken);
        var now = _dateTimeProvider.Now;

        var windows = teams
            .Select(t => TeamWindow.For(t, schedules.GetValueOrDefault(t.Id) ?? [], request.Start, request.End))
            .OfType<TeamWindow>()
            .ToList();
        if (windows.Count == 0)
            return [];

        var completions = await LoadCompletions(windows, cancellationToken);

        var current = windows.Where(w => w.From <= now && now < w.Until).ToList();
        var past = windows.Where(w => w.Until <= now).ToList();

        var inProgress = await LoadInProgressNow(current, cancellationToken);
        foreach (var group in past.GroupBy(w => w.Until))
            inProgress.AddRange(await LoadInProgressAt(group.Key, [.. group], cancellationToken));

        var incomplete = await LoadTeamsWithIncompleteHistory(past, cancellationToken);

        return [.. windows.Select(w =>
        {
            var completed = completions.Where(c => c.TeamId == w.TeamId && c.Done >= w.From && c.Done < w.Until).ToList();
            var active = inProgress.Where(i => i.TeamId == w.TeamId).ToList();
            var asOf = w.Until <= now ? w.Until : w.From <= now ? now : (Instant?)null;
            return Measure(w, asOf, completed, active, incomplete.Contains(w.TeamId));
        })];
    }

    private static KanbanTeamWindowMetricsDto Measure(TeamWindow window, Instant? asOf, List<Completion> completed, List<ActiveItem> active, bool isHistoryIncomplete)
    {
        var completedEstimates = completed.Select(c => WorkItemEstimate.Of(window.SizingMethod, c.StoryPoints, c.Effort, c.Size)).ToList();
        var activeEstimates = active.Select(a => WorkItemEstimate.Of(window.SizingMethod, a.StoryPoints, a.Effort, a.Size)).ToList();
        var cycled = completed.Where(c => c.Activated.HasValue && c.Done > c.Activated.Value).ToList();

        return new KanbanTeamWindowMetricsDto
        {
            Team = window.Team,
            From = window.From,
            Until = window.Until,
            TimeZone = window.TimeZone,
            SizingMethod = window.SizingMethod,
            CompletedWorkItems = completed.Count,
            CompletedEstimate = completedEstimates.Sum(e => e ?? 0),
            InProgressWorkItems = active.Count,
            InProgressEstimate = activeEstimates.Sum(e => e ?? 0),
            InProgressAsOf = asOf,
            UnestimatedWorkItems = completedEstimates.Concat(activeEstimates).Count(e => e is null),
            CycleTime = new CycleTimeSummary
            {
                WorkItemsCount = cycled.Count,
                TotalCycleTimeDays = cycled.Sum(c => (c.Done - c.Activated!.Value).ToTimeSpan().TotalDays),
            },
            IsHistoryIncomplete = isHistoryIncomplete,
        };
    }

    /// <summary>The teams' Requirement-tier items that reached a Done-category status inside any of the windows.</summary>
    private async Task<List<Completion>> LoadCompletions(List<TeamWindow> windows, CancellationToken cancellationToken)
    {
        var teamIds = windows.Select(w => w.TeamId).ToList();
        var from = windows.Min(w => w.From);
        var until = windows.Max(w => w.Until);

        // Removed items also carry a DoneTimestamp, so the status category is what excludes them.
        return await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Done)
            .Where(w => w.DoneTimestamp >= from && w.DoneTimestamp < until)
            .Select(w => new Completion(w.TeamId!.Value, w.DoneTimestamp!.Value, w.ActivatedTimestamp, w.StoryPoints, w.Effort, w.Size))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The teams' Requirement-tier items in an In Progress-category status now.</summary>
    private async Task<List<ActiveItem>> LoadInProgressNow(List<TeamWindow> windows, CancellationToken cancellationToken)
    {
        if (windows.Count == 0)
            return [];

        var teamIds = windows.Select(w => w.TeamId).ToList();
        return await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Active)
            .Select(w => new ActiveItem(w.TeamId!.Value, w.StoryPoints, w.Effort, w.Size))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The teams' items that were Requirement-tier and in an In Progress-category status just before
    /// <paramref name="at"/>, from work item history, with the estimates they had then.
    /// </summary>
    private async Task<List<ActiveItem>> LoadInProgressAt(Instant at, List<TeamWindow> windows, CancellationToken cancellationToken)
    {
        var teamIds = windows.Select(w => w.TeamId).ToList();

        // The period holding the instant just before `at` began before it and ended at or after it.
        return await (
            from h in _workDbContext.WorkItemStateHistory
            join w in _workDbContext.WorkItems on h.WorkItemId equals w.Id
            where w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value)
                && h.ValidFrom < at && (h.ValidTo == null || h.ValidTo >= at)
                && h.StatusCategory == WorkStatusCategory.Active
                // The type's tier now, not when the period was written: history keeps the type, not its level.
                && _workDbContext.WorkTypes.Any(t => t.Id == h.WorkTypeId && t.Level!.Tier == WorkTypeTier.Requirement)
            select new ActiveItem(w.TeamId!.Value, h.StoryPoints, h.Effort, h.Size))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The teams holding work in a workspace whose history has not been read through to the end.</summary>
    private async Task<HashSet<Guid>> LoadTeamsWithIncompleteHistory(List<TeamWindow> windows, CancellationToken cancellationToken)
    {
        if (windows.Count == 0)
            return [];

        var teamIds = windows.Select(w => w.TeamId).ToList();
        var incomplete = await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Workspace.WorkItemHistoryBackfilledOn == null)
            .Select(w => w.TeamId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. incomplete];
    }

    /// <summary>A Kanban team's window, its bounds counted in the time zone of the model in effect on each.</summary>
    private sealed record TeamWindow(NavigationDto Team, Instant From, Instant Until, string TimeZone, SizingMethod SizingMethod)
    {
        /// <summary>The team's id.</summary>
        public Guid TeamId => Team.Id;

        /// <summary>The team's window from <paramref name="start"/> to <paramref name="end"/>, or null when it is not Kanban then.</summary>
        public static TeamWindow? For(NavigationDto team, IReadOnlyList<TeamSchedulePeriodDto> schedule, LocalDate start, LocalDate end)
        {
            var atStart = schedule.FirstOrDefault(p => p.IncludesDate(start));
            var atEnd = schedule.FirstOrDefault(p => p.IncludesDate(end));
            var model = atStart ?? atEnd;
            if (model is null || model.Methodology != Methodology.Kanban)
                return null;

            var endZone = (atEnd ?? model).TimeZone;
            return new TeamWindow(
                team,
                start.AtStartOfDayInZone(DateTimeZoneProviders.Tzdb[model.TimeZone]).ToInstant(),
                end.PlusDays(1).AtStartOfDayInZone(DateTimeZoneProviders.Tzdb[endZone]).ToInstant(),
                endZone,
                model.SizingMethod);
        }
    }

    private sealed record Completion(Guid TeamId, Instant Done, Instant? Activated, double? StoryPoints, double? Effort, double? Size);

    private sealed record ActiveItem(Guid TeamId, double? StoryPoints, double? Effort, double? Size);
}
