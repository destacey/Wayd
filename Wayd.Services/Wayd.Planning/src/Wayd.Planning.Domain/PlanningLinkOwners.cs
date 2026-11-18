using Wayd.Common.Domain.Links;

namespace Wayd.Planning.Domain;

/// <summary>
/// The Planning records that can carry links.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so links carry no Planning vocabulary. The keys are persisted on every link:
/// <strong>never change one once shipped</strong>, or every link of that kind is orphaned.
/// </remarks>
public static class PlanningLinkOwners
{
    /// <summary>A planning interval.</summary>
    public static readonly LinkOwnerTypeDescriptor PlanningInterval =
        new("planning.planning-interval", "Planning Interval", "PlanningInterval");

    /// <summary>A team's objective for a planning interval. Spelled as its follow owner type.</summary>
    public static readonly LinkOwnerTypeDescriptor PlanningIntervalObjective =
        new(PlanningFollowOwners.PlanningIntervalObjective.Key, "Planning Interval Objective", "PlanningIntervalObjective");

    /// <summary>A risk.</summary>
    public static readonly LinkOwnerTypeDescriptor Risk = new("planning.risk", "Risk", "Risk");

    /// <summary>Every Planning owner type, for registration.</summary>
    public static readonly LinkOwnerTypeDescriptor[] All = [PlanningInterval, PlanningIntervalObjective, Risk];

    /// <summary>Registers the Planning owner types with <see cref="LinkOwnerTypes"/>. Idempotent.</summary>
    public static void Register() => LinkOwnerTypes.Register(All);
}
