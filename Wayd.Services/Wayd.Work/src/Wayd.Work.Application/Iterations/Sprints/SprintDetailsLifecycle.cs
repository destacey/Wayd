using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Work.Application.Iterations.Dtos;

namespace Wayd.Work.Application.Iterations.Sprints;

public static class SprintDetailsLifecycle
{
    /// <summary>
    /// Fills the fields of <paramref name="details"/> that depend on the team's other sprints and schedules,
    /// answered by the same timeline rules the lifecycle commands enforce, so the page offers only the actions
    /// they would accept.
    /// </summary>
    public static async Task Resolve(
        this SprintDetailsDto details,
        Iteration sprint,
        TeamSprintTimeline timeline,
        ICurrentPrincipal currentPrincipal,
        IDispatcher dispatcher,
        Instant now,
        CancellationToken cancellationToken)
    {
        details.State = SimpleNavigationDto.FromEnum(new IterationStateReader([timeline], timeline.DefaultZone, now).StateOf(sprint));

        // A sprint with no planned dates is outside the timeline and has no lifecycle.
        var entity = timeline.Sprints.FirstOrDefault(s => s.Id == sprint.Id);
        if (entity is null)
            return;

        details.ActiveFrom = timeline.ActiveFrom(entity);
        details.ActiveUntil = timeline.EffectiveEnd(entity);
        details.TimeZone = timeline.ScheduleFor(entity).TimeZone.Id;
        details.OverlapsPreviousSprint = timeline.OverlapsPrevious(entity);
        details.OverlapsNextSprint = timeline.OverlapsNext(entity);

        var startWindow = timeline.StartWindow(entity, now);
        details.CanStart = startWindow.IsSuccess;
        details.StartWindow = startWindow.IsSuccess ? InstantWindowDto.From(startWindow.Value, now) : null;

        var completeWindow = timeline.CompleteWindow(entity, now);
        details.CanComplete = completeWindow.IsSuccess;
        details.CompleteWindow = completeWindow.IsSuccess ? InstantWindowDto.From(completeWindow.Value, now) : null;
        details.CanReopen = timeline.CanReopen(entity).IsSuccess;

        if (timeline.OpenSprint is { } open && open.Id != entity.Id)
            details.OpenSprint = NavigationDto.Create(open.Id, open.Key, open.Name);

        details.CanManageSprint = await currentPrincipal.CanManageTeamSprints(dispatcher, timeline.TeamId, now.InUtc().Date, cancellationToken);
    }
}
