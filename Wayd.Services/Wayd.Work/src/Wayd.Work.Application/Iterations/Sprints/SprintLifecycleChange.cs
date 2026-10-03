using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Sprints;

/// <summary>
/// The steps every sprint lifecycle command shares: load the sprint and its team's timeline, check the caller
/// may change it, apply the change and save the sprints it touched together.
/// </summary>
internal static class SprintLifecycleChange
{
    /// <summary>
    /// The domain calls a change makes, in order. Each is saved before the next runs, inside one transaction
    /// when there are several, so the database sees them in that order but keeps all or none.
    /// </summary>
    public delegate IReadOnlyList<Func<Result>> Change(Iteration sprint, TeamSprintTimeline timeline, EventActor actor, Instant now);

    public static async Task<Result> Apply(
        IWorkDbContext workDbContext,
        IDispatcher dispatcher,
        ISettings<SchedulingSettings> schedulingSettings,
        ICurrentUser currentUser,
        ICurrentPrincipal currentPrincipal,
        IDateTimeProvider dateTimeProvider,
        ILogger logger,
        Guid sprintId,
        string action,
        Change change,
        CancellationToken cancellationToken)
    {
        var sprint = await workDbContext.Iterations
            .FirstOrDefaultAsync(i => i.Id == sprintId && i.Type == IterationType.Sprint, cancellationToken);

        if (sprint is null)
        {
            logger.LogInformation("Sprint {SprintId} not found.", sprintId);
            return Result.Failure("Sprint not found.");
        }

        if (sprint.TeamId is not { } teamId)
            return Result.Failure("The sprint's team is not mapped to a Wayd team.");

        var now = dateTimeProvider.Now;

        if (!await currentPrincipal.CanManageTeamSprints(dispatcher, teamId, now.InUtc().Date, cancellationToken))
        {
            logger.LogInformation("The caller may not {Action} sprint {SprintId}.", action, sprintId);
            return Result.Failure(SprintAuthorization.NotAMemberError);
        }

        var timeline = await workDbContext.LoadTeamSprintTimeline(dispatcher, schedulingSettings, teamId, tracked: true, cancellationToken);

        // Read per scope rather than from the claim snapshot, which a personal access token freezes for its
        // whole lifetime, and the actor is recorded on the event.
        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(currentUser.GetUserId(), employeeId);

        var steps = change(sprint, timeline, actor, now);

        await using var unitOfWork = steps.Count > 1
            ? await workDbContext.BeginUnitOfWork(cancellationToken)
            : null;

        foreach (var step in steps)
        {
            var result = step();
            if (result.IsFailure)
            {
                foreach (var teamSprint in timeline.Sprints)
                    teamSprint.ClearDomainEvents();

                logger.LogInformation("Unable to {Action} sprint {SprintId}. Error message: {Error}", action, sprintId, result.Error);
                return result;
            }

            try
            {
                await workDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The open-sprint index refuses a second open sprint for the team, which two concurrent starts
                // could otherwise both pass the aggregate's check to create.
                logger.LogWarning(ex, "Unable to save the {Action} of sprint {SprintId}.", action, sprintId);
                return Result.Failure("Another change to this team's sprints was saved at the same time. Refresh and try again.");
            }
        }

        if (unitOfWork is not null)
            await unitOfWork.CommitAsync(cancellationToken);

        logger.LogInformation("Sprint {SprintId}: {Action} recorded.", sprintId, action);
        return Result.Success();
    }
}
