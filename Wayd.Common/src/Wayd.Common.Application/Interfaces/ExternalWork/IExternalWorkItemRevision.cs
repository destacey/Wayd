namespace Wayd.Common.Application.Interfaces.ExternalWork;

/// <summary>
/// One revision of a work item as an external system recorded it: the tracked fields' values after
/// the revision, not what it changed. Wayd collapses consecutive revisions that change no tracked
/// field into one period of <c>WorkItemStateHistory</c>.
/// </summary>
/// <remarks>
/// Every value is what the source recorded at that revision, by the source's stable identifiers.
/// Nothing is resolved through a connection mapping here, because mappings are not effective-dated:
/// resolving through one would let a later full sync rewrite the past.
/// </remarks>
public interface IExternalWorkItemRevision
{
    /// <summary>The work item's id in the external system.</summary>
    int WorkItemId { get; }

    /// <summary>
    /// The source's revision number. Increases with every revision of the item, so Wayd can skip a
    /// revision it has already applied.
    /// </summary>
    int Revision { get; }

    /// <summary>When the revision was made.</summary>
    Instant Changed { get; }

    /// <summary>The work type name as the source recorded it.</summary>
    string WorkType { get; }

    /// <summary>The status name as the source recorded it.</summary>
    string WorkStatus { get; }

    /// <summary>
    /// The source's stable id for the item's iteration, never its path, which changes on rename or
    /// move; null when the item is in no iteration.
    /// </summary>
    int? IterationId { get; }

    /// <summary>
    /// The source's own value for a team recorded on the item, such as Jira's Team field; null for
    /// a source whose team comes from the iteration, as Azure DevOps' does.
    /// </summary>
    string? TeamKey { get; }

    /// <summary>The person the item was assigned to; null when unassigned.</summary>
    IExternalUserRef? AssignedTo { get; }

    /// <summary>The story points estimate; null when the item had none.</summary>
    double? StoryPoints { get; }

    /// <summary>The effort estimate; null when the item had none.</summary>
    double? Effort { get; }

    /// <summary>The size estimate; null when the item had none.</summary>
    double? Size { get; }
}
