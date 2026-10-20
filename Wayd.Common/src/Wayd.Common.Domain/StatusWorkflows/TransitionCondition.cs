using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// Something a move along a <see cref="WorkflowTransition"/> needs before a record may make it.
/// </summary>
/// <remarks>
/// An entity with its own id rather than a bare kind on the transition, so a kind that later needs
/// settings of its own (which approval policy applies, say) has somewhere to keep them. Created and
/// removed through the owning <see cref="StatusWorkflow"/>, never directly; it goes when its transition
/// goes.
/// </remarks>
public sealed class TransitionCondition : BaseAuditableEntity
{
    private TransitionCondition() { }

    internal TransitionCondition(Guid transitionId, TransitionConditionKind kind)
    {
        TransitionId = transitionId;
        Kind = kind;
    }

    /// <summary>The transition this condition applies to.</summary>
    public Guid TransitionId { get; private init; }

    /// <summary>What the move needs. Unique per transition.</summary>
    public TransitionConditionKind Kind { get; private init; }
}
