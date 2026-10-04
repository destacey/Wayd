using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Sprints;

public static class TeamSprintTimelineLoader
{
    /// <summary>
    /// Loads every sprint of the team with the schedules they are counted in. Tracked for a command, so the
    /// sprint it changes is the same instance as the one in the timeline; untracked for a read, with each
    /// sprint's team loaded so the sprints map straight to DTOs.
    /// </summary>
    public static async Task<TeamSprintTimeline> LoadTeamSprintTimeline(
        this IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        Guid teamId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = workDbContext.Iterations
            .Where(i => i.TeamId == teamId && i.Type == IterationType.Sprint);

        var sprints = tracked
            ? await query.ToListAsync(cancellationToken)
            : await query.Include(i => i.Team).AsNoTracking().ToListAsync(cancellationToken);

        var periods = await dispatcher.Send(new GetTeamScheduleHistoryQuery(teamId), cancellationToken);
        var defaults = await schedulingSettings.Get(cancellationToken);

        return new TeamSprintTimeline(teamId, sprints, Schedules(periods, defaults));
    }

    /// <summary>
    /// Reads the sprints and schedules of <paramref name="teamIds"/>, one query for each, to work out iteration
    /// states at <paramref name="now"/>. An iteration of any other team is read from its planned days.
    /// </summary>
    public static async Task<IterationStateReader> LoadIterationStateReader(
        this IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        IEnumerable<Guid> teamIds,
        Instant now,
        CancellationToken cancellationToken)
    {
        var distinctTeamIds = teamIds.Distinct().ToList();
        List<Iteration> sprints = distinctTeamIds.Count == 0
            ? []
            : await workDbContext.Iterations
                .Where(i => i.TeamId.HasValue && distinctTeamIds.Contains(i.TeamId.Value) && i.Type == IterationType.Sprint)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        return await dispatcher.BuildIterationStateReader(schedulingSettings, sprints, now, cancellationToken);
    }

    /// <summary>
    /// Works out iteration states at <paramref name="now"/> from sprints already read. The caller passes every
    /// sprint of each team it includes, since a sprint's state depends on its neighbours.
    /// </summary>
    public static async Task<IterationStateReader> BuildIterationStateReader(
        this IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        IReadOnlyCollection<Iteration> teamSprints,
        Instant now,
        CancellationToken cancellationToken)
    {
        var defaults = await schedulingSettings.Get(cancellationToken);
        var teamIds = teamSprints
            .Where(s => s.Type == IterationType.Sprint && s.TeamId.HasValue)
            .Select(s => s.TeamId!.Value)
            .Distinct()
            .ToList();
        if (teamIds.Count == 0)
            return new IterationStateReader([], Zone(defaults.DefaultTimeZone), now);

        var periods = await dispatcher.Send(new GetTeamsScheduleHistoryQuery(teamIds), cancellationToken);

        var timelines = teamIds.Select(teamId => new TeamSprintTimeline(
            teamId,
            teamSprints.Where(s => s.TeamId == teamId),
            Schedules(periods.GetValueOrDefault(teamId) ?? [], defaults)));

        return new IterationStateReader(timelines, Zone(defaults.DefaultTimeZone), now);
    }

    private static TeamSprintSchedules Schedules(IEnumerable<TeamSchedulePeriodDto> periods, SchedulingSettings defaults) =>
        new(
            periods.Select(p => new SprintSchedulePeriod(p.Start, p.End, new SprintSchedule(Zone(p.TimeZone), p.CommitmentGraceDays))),
            new SprintSchedule(Zone(defaults.DefaultTimeZone), defaults.DefaultCommitmentGraceDays));

    // Zones are validated when saved; an id the tz database later drops falls back rather than failing
    // every read of the team's sprints.
    private static DateTimeZone Zone(string id) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) ?? DateTimeZone.Utc;
}
