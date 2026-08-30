namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// The lifecycle of a workflow definition itself, distinct from the statuses it contains.
/// </summary>
/// <remarks>
/// A tiny, fixed lifecycle on a configuration entity — deliberately an enum rather than something the
/// engine governs itself, which would be circular. Mirrors <c>ScoringModelState</c>.
/// </remarks>
public enum StatusWorkflowState
{
    /// <summary>Being built. Statuses can be added, removed and reordered freely; not yet assignable.</summary>
    Draft = 1,

    /// <summary>
    /// Available to assign. Says nothing about whether anything uses it — a published workflow with no
    /// assignment is exactly the one an administrator switches a scope onto.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Withdrawn from use. Cannot be assigned; existing records still resolve their statuses through it.
    /// </summary>
    Archived = 3
}
