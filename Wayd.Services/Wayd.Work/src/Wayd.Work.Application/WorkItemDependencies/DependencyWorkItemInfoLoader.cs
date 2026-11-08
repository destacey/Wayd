using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkItemDependencies;

/// <summary>
/// Builds the <see cref="DependencyWorkItemInfo"/> that dependency health is calculated from, resolving each
/// end's sprint time zone, which the domain cannot look up itself.
/// </summary>
public static class DependencyWorkItemInfoLoader
{
    /// <summary>
    /// Reads what a dependency needs from each of <paramref name="workItemIds"/> at <paramref name="now"/>, keyed
    /// by work item id. Whether an item's sprint has ended is read in that sprint's team's time zone, so a
    /// dependency's health reads the same wherever it is shown. Ids with no work item are left out.
    /// </summary>
    public static async Task<Dictionary<Guid, DependencyWorkItemInfo>> LoadDependencyWorkItemInfos(
        this IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        ISettings<GeneralSettings> generalSettings,
        IReadOnlyCollection<Guid> workItemIds,
        Instant now,
        CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
            return [];

        var items = await workDbContext.WorkItems
            .Where(wi => workItemIds.Contains(wi.Id))
            .Select(wi => new
            {
                wi.Id,
                wi.StatusCategory,
                SprintId = wi.Iteration != null && wi.Iteration.Type == IterationType.Sprint ? wi.IterationId : null,
                TeamId = wi.Iteration != null ? wi.Iteration.TeamId : null,
                Start = wi.Iteration != null ? wi.Iteration.DateRange.Start : null,
                End = wi.Iteration != null ? wi.Iteration.DateRange.End : null,
            })
            .ToListAsync(cancellationToken);

        var sprints = items
            .Where(i => i.SprintId.HasValue && i.End.HasValue)
            .Select(i => (Id: i.SprintId!.Value, i.TeamId, PlannedStart: i.Start))
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, SprintSchedule> schedules = sprints.Count == 0
            ? new Dictionary<Guid, SprintSchedule>()
            : await dispatcher.LoadSprintSchedules(schedulingSettings, generalSettings, sprints, cancellationToken);

        return items.ToDictionary(
            i => i.Id,
            i => i.SprintId is { } sprintId && i.End is { } end
                ? DependencyWorkItemInfo.Create(i.Id, i.StatusCategory, end, schedules[sprintId].TimeZone, now)
                : DependencyWorkItemInfo.Create(i.Id, i.StatusCategory));
    }
}
