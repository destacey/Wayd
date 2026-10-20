namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// What a move along a <see cref="WorkflowTransition"/> needs before a record may make it.
/// </summary>
/// <remarks>
/// A closed set: each kind is a rule the engine knows how to enforce, so an organization chooses among
/// them rather than inventing its own. A transition holds at most one condition of each kind.
/// <para>
/// Some kinds the record can check for itself, from what the move carries
/// (<see cref="TransitionConditionKinds.IsSelfChecked"/>). The rest need data the record does not hold
/// and are evaluated in the application layer before the move, which reports the result on the
/// <see cref="StatusRef"/>.
/// </para>
/// </remarks>
public enum TransitionConditionKind
{
    /// <summary>
    /// The move must carry a non-empty reason, recorded on the <see cref="StatusTransition"/>.
    /// </summary>
    RequiresReason = 1,

    /// <summary>
    /// The record must hold a passed approval for the move. Reserved for approvals: nothing evaluates it
    /// yet, so it cannot be attached to a transition until something does.
    /// </summary>
    RequiresPassedApproval = 2,
}

/// <summary>What the engine knows about each <see cref="TransitionConditionKind"/>.</summary>
public static class TransitionConditionKinds
{
    /// <summary>
    /// Whether a record can check the condition itself from what the move carries, without the
    /// application layer evaluating it first.
    /// </summary>
    public static bool IsSelfChecked(this TransitionConditionKind kind) =>
        kind == TransitionConditionKind.RequiresReason;

    /// <summary>
    /// What a move missing this condition lacks, phrased to follow "needs" in a refusal.
    /// </summary>
    public static string DescribeMissing(this TransitionConditionKind kind) => kind switch
    {
        TransitionConditionKind.RequiresReason => "a reason",
        TransitionConditionKind.RequiresPassedApproval => "a passed approval",
        _ => kind.ToString(),
    };
}
