using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.Iterations.Queries;

/// <summary>
/// The sprint signal of each of <paramref name="TeamIds"/>, keyed by team id: a sprint the team started and left
/// open past its end, or else its running sprint's health against its commitment. A team with neither, between
/// sprints or with no sprints, is missing.
/// </summary>
public sealed record GetTeamsSprintSignalQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, TeamSprintSignalDto>>;

/// <summary>Handles <see cref="GetTeamsSprintSignalQuery"/>.</summary>
public sealed class GetTeamsSprintSignalQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ISettings<GeneralSettings> generalSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamsSprintSignalQuery, IReadOnlyDictionary<Guid, TeamSprintSignalDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ISettings<GeneralSettings> _generalSettings = generalSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, TeamSprintSignalDto>> Handle(GetTeamsSprintSignalQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var signals = new Dictionary<Guid, TeamSprintSignalDto>();

        // Each team's timeline and its running sprint's history are their own reads; a team of teams has few teams.
        foreach (var teamId in request.TeamIds.Distinct())
        {
            var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, _generalSettings, teamId, tracked: false, cancellationToken);

            if (timeline.OpenSprint is { } open && timeline.StateAt(open, now) == IterationState.Completed)
            {
                signals[teamId] = new TeamSprintSignalDto
                {
                    Sprint = Navigation(open),
                    EndedNotCompleted = true,
                    Status = AttentionStatus.NeedsAttention,
                };
                continue;
            }

            var active = timeline.Sprints.FirstOrDefault(s => timeline.StateAt(s, now) == IterationState.Active);
            if (active is null)
                continue;

            var health = await MeasureHealth(timeline, active, now, cancellationToken);
            signals[teamId] = new TeamSprintSignalDto
            {
                Sprint = Navigation(active),
                ExpectedShare = health?.ExpectedShare,
                DoneShare = health?.DoneShare,
                Status = health?.Status ?? AttentionStatus.OnTrack,
            };
        }

        return signals;
    }

    /// <summary>
    /// The sprint's health as its sprint card works it out: against its commitment once its history is read in full
    /// and its commitment point has passed; with nothing committed, its scope done against its days gone; and before
    /// then, the work in it now done against its days gone.
    /// </summary>
    private async Task<SprintHealth?> MeasureHealth(TeamSprintTimeline timeline, Iteration sprint, Instant now, CancellationToken cancellationToken)
    {
        var zone = timeline.ScheduleFor(sprint).TimeZone;
        var firstDay = timeline.ActiveFrom(sprint).InZone(zone).Date;
        // The effective end is exclusive, so the last day is the one the moment before it falls on.
        var lastDay = timeline.EffectiveEnd(sprint).Minus(Duration.Epsilon).InZone(zone).Date;
        if (lastDay < firstDay)
            lastDay = firstDay;
        var today = now.InZone(zone).Date;

        var history = await _workDbContext.LoadSprintScopeHistory(_dispatcher, timeline, sprint, cancellationToken);
        if (history is not null && !history.HistoryIncomplete && now >= history.Window.Start)
        {
            var totals = SprintScopeReport.Build(history.Window, history.SizingMethod, history.Periods, now).Totals;
            return totals.Committed.Estimate > 0
                ? SprintHealth.Measure(history.Window, SprintIdealLine.Build(history.Window, history.WorkingDays), totals.Committed.Estimate, totals.Completed.Estimate, now)
                : SprintHealth.MeasureOverDays(firstDay, lastDay, today, totals.Total.Estimate - totals.Descoped.Estimate, totals.Completed.Estimate);
        }

        // Requirement-tier work only, as the sprint's metrics count it.
        var sizingMethod = timeline.ScheduleFor(sprint).SizingMethod;
        var items = await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprint.Id && w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .ToListAsync(cancellationToken);
        var total = items.Sum(w => WorkItemEstimate.Of(sizingMethod, w) ?? 0);
        var completed = items
            .Where(w => w.StatusCategory is StatusCategory.Done or StatusCategory.Removed)
            .Sum(w => WorkItemEstimate.Of(sizingMethod, w) ?? 0);

        return SprintHealth.MeasureOverDays(firstDay, lastDay, today, total, completed);
    }

    /// <summary>The sprint as the signal links to it.</summary>
    private static NavigationDto Navigation(Iteration sprint) =>
        new() { Id = sprint.Id, Key = sprint.Key, Name = sprint.Name };
}
