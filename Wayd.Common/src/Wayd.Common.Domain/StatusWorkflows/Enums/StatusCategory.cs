using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// The high-level bucket a status belongs to, for workflow statuses and work item statuses alike.
/// Deliberately minimal and closed: organizations add and rename <em>statuses</em> freely, but never
/// categories, because this is the vocabulary every domain invariant and cross-module rollup is written
/// against.
/// </summary>
/// <remarks>
/// Stored by name (at most 32 characters), so <strong>never rename a value</strong>. Like
/// <see cref="LifecycleCategory"/>, these are unordered identifiers: do not infer direction by comparing
/// them.
/// </remarks>
public enum StatusCategory
{
    /// <summary>Not yet started — the work or record is queued, proposed, or awaiting approval.</summary>
    [Display(Name = "Proposed", Description = "Not yet started.", Order = 1)]
    Proposed = 0,

    /// <summary>Underway. Includes paused states such as On Hold, which are active but not progressing.</summary>
    [Display(Name = "Active", Description = "In progress.", Order = 2)]
    Active = 1,

    /// <summary>Reached its intended end.</summary>
    [Display(Name = "Done", Description = "Completed.", Order = 3)]
    Done = 2,

    /// <summary>Ended without reaching its intended end — cancelled, withdrawn, abandoned.</summary>
    [Display(Name = "Removed", Description = "Ended without being completed.", Order = 4)]
    Removed = 3
}
