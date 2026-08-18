namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// Which earlier statuses a project may be reverted to, and what each status requires on entry.
/// </summary>
/// <remarks>
/// The backward moves are an explicit table, never a comparison. <see cref="ProjectStatus"/> values are
/// not in lifecycle order (Approved = 5 sits between Proposed = 1 and Active = 2), so <c>to &lt; from</c>
/// compares declaration order. Canceled is also a branch off the lifecycle rather than a late stage, so
/// no ordering places it correctly.
/// </remarks>
public static class ProjectStatusLifecycle
{
    private static readonly IReadOnlyDictionary<ProjectStatus, IReadOnlyList<ProjectStatus>> BackwardTargets =
        new Dictionary<ProjectStatus, IReadOnlyList<ProjectStatus>>
        {
            [ProjectStatus.Completed] = [ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active],
            [ProjectStatus.Canceled] = [ProjectStatus.Proposed, ProjectStatus.Approved, ProjectStatus.Active],
            [ProjectStatus.Active] = [ProjectStatus.Proposed, ProjectStatus.Approved],
            [ProjectStatus.Approved] = [ProjectStatus.Proposed],
            [ProjectStatus.Proposed] = []
        };

    /// <summary>
    /// The statuses <paramref name="current"/> may be reverted to, in lifecycle order. Empty when the
    /// project is already at the start of its lifecycle.
    /// </summary>
    public static IReadOnlyList<ProjectStatus> BackwardTargetsFor(ProjectStatus current) =>
        BackwardTargets.TryGetValue(current, out var targets) ? targets : [];

    /// <summary>
    /// Whether moving from <paramref name="from"/> to <paramref name="to"/> is a backward transition.
    /// False for a forward move and for a move to the same status, so a caller that checks this cannot
    /// reach the no-op guard on <see cref="Models.ProjectStatusHistory"/>.
    /// </summary>
    public static bool IsBackwardTransition(ProjectStatus from, ProjectStatus to) =>
        BackwardTargetsFor(from).Contains(to);

    /// <summary>
    /// Whether a project with the given lifecycle and timeline satisfies the requirements for being in
    /// <paramref name="status"/>, independent of the status it is coming from.
    /// </summary>
    /// <remarks>
    /// Must stay in step with the preconditions on <c>Project.Approve</c> and <c>Project.Activate</c>,
    /// including their asymmetry: activating requires a timeline but deliberately does not require a
    /// lifecycle.
    /// </remarks>
    /// <param name="status">The status being entered.</param>
    /// <param name="hasLifecycle">Whether a project lifecycle is assigned.</param>
    /// <param name="hasDateRange">Whether a start and end date are set.</param>
    public static bool CanEnter(ProjectStatus status, bool hasLifecycle, bool hasDateRange) => status switch
    {
        ProjectStatus.Approved => hasLifecycle,
        ProjectStatus.Active => hasDateRange,
        _ => true
    };

    /// <summary>
    /// The statuses a project in <paramref name="current"/> can actually be reverted to — the backward
    /// targets it also meets the entry requirements for, in lifecycle order.
    /// </summary>
    /// <param name="current">The project's current status.</param>
    /// <param name="hasLifecycle">Whether a project lifecycle is assigned.</param>
    /// <param name="hasDateRange">Whether a start and end date are set.</param>
    public static IReadOnlyList<ProjectStatus> RevertableStatuses(ProjectStatus current, bool hasLifecycle, bool hasDateRange) =>
        [.. BackwardTargetsFor(current).Where(s => CanEnter(s, hasLifecycle, hasDateRange))];
}
