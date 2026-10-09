using System.Linq.Expressions;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.Iterations.Sprints;

/// <summary>
/// What sprint scope and the sprint's burn are both worked out from: the sprint's window and schedule, and the
/// history of every item that was in it during that window.
/// </summary>
/// <param name="Schedule">The schedule the sprint is counted in.</param>
/// <param name="WorkingDays">Which of the sprint's days the team works, which its ideal burn-down falls on.</param>
/// <param name="HistoryIncomplete">
/// Whether a workspace holding the sprint's work has not had its history read through to the end, so the
/// history may be missing changes.
/// </param>
public sealed record SprintScopeHistory(
    Iteration Sprint,
    SprintScopeWindow Window,
    SprintSchedule Schedule,
    SprintWorkingDays WorkingDays,
    List<SprintScopePeriod> Periods,
    bool HistoryIncomplete)
{
    /// <summary>The estimate the sprint is measured in.</summary>
    public SizingMethod SizingMethod => Schedule.SizingMethod;
}

/// <summary>
/// Loads what sprint scope and the sprint's burn are worked out from, so the two read the same window and the
/// same work item history.
/// </summary>
public static class SprintScopeHistoryLoader
{
    /// <summary>Loads the sprint matching <paramref name="sprintFilter"/>; null when there is none, or it has no planned dates.</summary>
    public static async Task<SprintScopeHistory?> LoadSprintScopeHistory(
        this IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        Expression<Func<Iteration, bool>> sprintFilter,
        CancellationToken cancellationToken)
    {
        var sprint = await workDbContext.Iterations
            .Where(sprintFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null || sprint.DateRange.Start is null || sprint.DateRange.End is null)
            return null;

        var (window, schedule) = await LoadWindow(workDbContext, dispatcher, schedulingSettings, sprint, cancellationToken);
        var periods = await LoadPeriods(workDbContext, window, cancellationToken);
        var workItemIds = periods.Select(p => p.WorkItemId).Distinct().ToList();

        return new SprintScopeHistory(
            sprint,
            window,
            schedule,
            await LoadWorkingDays(dispatcher, sprint, window, schedule, cancellationToken),
            periods,
            await IsHistoryIncomplete(workDbContext, sprint.Id, workItemIds, cancellationToken));
    }

    private static async Task<(SprintScopeWindow Window, SprintSchedule Schedule)> LoadWindow(
        IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        Iteration sprint,
        CancellationToken cancellationToken)
    {
        if (sprint.TeamId is { } teamId)
        {
            var timeline = await workDbContext.LoadTeamSprintTimeline(dispatcher, schedulingSettings, teamId, tracked: false, cancellationToken);
            var onTimeline = timeline.Sprints.Single(s => s.Id == sprint.Id);
            return (SprintScopeWindow.For(timeline, onTimeline), timeline.ScheduleFor(onTimeline));
        }

        var schedules = await dispatcher.LoadSprintSchedules(
            schedulingSettings,
            [(sprint.Id, sprint.TeamId, sprint.DateRange.Start)],
            cancellationToken);
        var schedule = schedules[sprint.Id];
        return (SprintScopeWindow.Unscheduled(sprint, schedule), schedule);
    }

    /// <summary>
    /// The team's working week, less its calendar's holidays and the sprint's team days off, over the days the
    /// window touches in the team's zone. Team days off are kept only within the planned dates they were set in.
    /// </summary>
    private static async Task<SprintWorkingDays> LoadWorkingDays(
        IDispatcher dispatcher,
        Iteration sprint,
        SprintScopeWindow window,
        SprintSchedule schedule,
        CancellationToken cancellationToken)
    {
        var from = window.Start.InZone(window.TimeZone).Date;
        var to = window.End.InZone(window.TimeZone).Date;

        var holidays = await dispatcher.Send(new GetHolidayDatesQuery(schedule.HolidayCalendarId, from, to), cancellationToken);
        var teamDaysOff = sprint.TeamDaysOff.Where(d => sprint.DateRange.Includes(d));

        return new SprintWorkingDays(schedule.WorkingWeek, holidays.Concat(teamDaysOff));
    }

    /// <summary>
    /// The history, from the commitment point to the effective end, of every item that was in the sprint
    /// between them. The period an item left into is included, since where it went decides its outcome.
    /// </summary>
    private static async Task<List<SprintScopePeriod>> LoadPeriods(IWorkDbContext workDbContext, SprintScopeWindow window, CancellationToken cancellationToken)
    {
        var sprintId = window.SprintId;
        var start = window.Start;
        var end = window.End;

        var workItemIds = workDbContext.WorkItemStateHistory
            .Where(h => h.IterationId == sprintId && h.ValidFrom < end && (h.ValidTo == null || h.ValidTo > start))
            .Select(h => h.WorkItemId)
            .Distinct();

        return await workDbContext.WorkItemStateHistory
            .Where(h => workItemIds.Contains(h.WorkItemId) && h.ValidFrom <= end && (h.ValidTo == null || h.ValidTo > start))
            .Select(h => new SprintScopePeriod(
                h.WorkItemId,
                h.ValidFrom,
                h.ValidTo,
                h.IterationId,
                // The type's tier now, not when the period was written: history keeps the type, not its level.
                workDbContext.WorkTypes.Any(t => t.Id == h.WorkTypeId && t.Level!.Tier == WorkTypeTier.Requirement),
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
    private static async Task<bool> IsHistoryIncomplete(IWorkDbContext workDbContext, Guid sprintId, List<Guid> workItemIds, CancellationToken cancellationToken) =>
        await workDbContext.WorkItems
            .Where(w => w.IterationId == sprintId || workItemIds.Contains(w.Id))
            .AnyAsync(w => w.Workspace.WorkItemHistoryBackfilledOn == null, cancellationToken);
}
