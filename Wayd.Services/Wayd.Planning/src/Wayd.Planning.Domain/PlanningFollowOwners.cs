using Wayd.Common.Domain.Following;

namespace Wayd.Planning.Domain;

/// <summary>
/// The Planning records people can follow.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so following carries no Planning vocabulary. The keys are
/// persisted on every follow row: <strong>never change one once shipped</strong>, or every follow of that
/// kind is orphaned.
/// </remarks>
public static class PlanningFollowOwners
{
    /// <summary>A team's objective for a planning interval.</summary>
    public static readonly FollowOwnerDescriptor PlanningIntervalObjective =
        new("planning.planning-interval-objective", "Planning Interval Objective");

    /// <summary>Every Planning owner type, for registration.</summary>
    public static readonly FollowOwnerDescriptor[] All = [PlanningIntervalObjective];

    /// <summary>Registers the Planning owner types with <see cref="FollowOwners"/>. Idempotent.</summary>
    public static void Register() => FollowOwners.Register(All);
}
