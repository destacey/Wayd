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
    /// sprint it changes is the same instance as the one in the timeline.
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
            : await query.AsNoTracking().ToListAsync(cancellationToken);

        var periods = await dispatcher.Send(new GetTeamScheduleHistoryQuery(teamId), cancellationToken);
        var defaults = await schedulingSettings.Get(cancellationToken);

        var schedules = new TeamSprintSchedules(
            periods.Select(p => new SprintSchedulePeriod(p.Start, p.End, new SprintSchedule(Zone(p.TimeZone), p.CommitmentGraceDays))),
            new SprintSchedule(Zone(defaults.DefaultTimeZone), defaults.DefaultCommitmentGraceDays));

        return new TeamSprintTimeline(teamId, sprints, schedules);
    }

    // Zones are validated when saved; an id the tz database later drops falls back rather than failing
    // every read of the team's sprints.
    private static DateTimeZone Zone(string id) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) ?? DateTimeZone.Utc;
}
