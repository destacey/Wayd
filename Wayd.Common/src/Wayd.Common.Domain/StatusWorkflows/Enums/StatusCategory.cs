namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// The high-level bucket a workflow status belongs to. Deliberately minimal and closed: organizations
/// add and rename <em>statuses</em> freely, but never categories, because this is the vocabulary every
/// domain invariant and cross-workflow comparison is written against.
/// </summary>
/// <remarks>
/// Matches <c>WorkStatusCategory</c> so that a status mirrored from an external tracker and one defined
/// by an administrator can be reasoned about the same way. Like <see cref="Enums.LifecycleCategory"/>,
/// these are unordered identifiers: do not infer direction by comparing them.
/// </remarks>
public enum StatusCategory
{
    /// <summary>Not yet started — the work or record is queued, proposed, or awaiting approval.</summary>
    Proposed = 0,

    /// <summary>Underway. Includes paused states such as On Hold, which are active but not progressing.</summary>
    Active = 1,

    /// <summary>Reached its intended end.</summary>
    Done = 2,

    /// <summary>Ended without reaching its intended end — cancelled, withdrawn, abandoned.</summary>
    Removed = 3
}
