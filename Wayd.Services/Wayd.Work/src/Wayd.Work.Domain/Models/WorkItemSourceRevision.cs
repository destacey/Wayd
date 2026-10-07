using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// One revision of a work item as its source recorded it: the tracked fields' values after the
/// revision, in the source's own terms. Every revision read is kept, so a work item's
/// <see cref="WorkItemStateHistory"/> can be rebuilt from them whatever order they arrived in.
/// </summary>
/// <remarks>
/// Revisions arrive out of order when an item moved between projects: each project's revisions are
/// read separately, and a revision made in one can arrive after later ones made in another. Periods
/// merge revisions that change nothing tracked, so they cannot place a late revision; this log can.
/// Only source values are stored: they are resolved to Wayd's records when periods are built.
/// Imported, not raised by Wayd, so it raises no domain events; the rows are deleted with their work item.
/// </remarks>
public sealed class WorkItemSourceRevision : BaseEntity<long>
{
    private WorkItemSourceRevision() { }

    private WorkItemSourceRevision(Guid workItemId, Guid workspaceId, int revision, Instant changed, WorkItemSourceValues values)
    {
        WorkItemId = workItemId;
        WorkspaceId = workspaceId;
        Revision = revision;
        Changed = changed;

        ExternalIterationId = values.ExternalIterationId;
        StatusName = values.StatusName;
        WorkTypeName = values.WorkTypeName;
        TeamKey = values.TeamKey;
        AssignedToExternalId = values.AssignedToExternalId;
        StoryPoints = values.StoryPoints;
        Effort = values.Effort;
        Size = values.Size;
    }

    /// <summary>The work item the revision belongs to.</summary>
    public Guid WorkItemId { get; private init; }

    /// <summary>
    /// The workspace whose sync read the revision. A revision fetched from the item to fill a gap,
    /// made in a project or as a work type no synced workspace covers, is recorded under the
    /// workspace the item is in.
    /// </summary>
    public Guid WorkspaceId { get; private init; }

    /// <summary>The source's revision number, which orders an item's revisions with no gaps.</summary>
    public int Revision { get; private init; }

    /// <summary>When the source recorded the revision.</summary>
    public Instant Changed { get; private init; }

    /// <summary>The source's stable id for the iteration; null when the item was in none.</summary>
    public int? ExternalIterationId { get; private init; }

    /// <summary>The status name as the source recorded it.</summary>
    public string StatusName { get; private init; } = null!;

    /// <summary>The work type name as the source recorded it.</summary>
    public string WorkTypeName { get; private init; } = null!;

    /// <summary>The source's own value for a team recorded on the item; null for a source whose team comes from the iteration.</summary>
    public string? TeamKey { get; private init; }

    /// <summary>The source's identity id for the assignee; null when unassigned.</summary>
    public string? AssignedToExternalId { get; private init; }

    /// <summary>The story points estimate.</summary>
    public double? StoryPoints { get; private init; }

    /// <summary>The effort estimate.</summary>
    public double? Effort { get; private init; }

    /// <summary>The size estimate.</summary>
    public double? Size { get; private init; }

    /// <summary>The source values this revision recorded.</summary>
    public WorkItemSourceValues Values => new(
        ExternalIterationId, StatusName, WorkTypeName, TeamKey, AssignedToExternalId, StoryPoints, Effort, Size);

    /// <summary>Records a revision read from the source.</summary>
    public static WorkItemSourceRevision Create(Guid workItemId, Guid workspaceId, int revision, Instant changed, WorkItemSourceValues values) =>
        new(workItemId, workspaceId, revision, changed, values);
}
