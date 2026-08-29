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

    /// <summary>Assignable and in use. Safe edits only — records may already hold its statuses.</summary>
    Active = 2,

    /// <summary>Withdrawn from use. Retained so historical records keep resolving their status.</summary>
    Archived = 3
}
