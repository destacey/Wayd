using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Applies one work item's source revisions to its <see cref="WorkItemStateHistory"/>, collapsing
/// revisions that change no tracked field into the period already open.
/// </summary>
/// <remarks>
/// Revisions must be applied in revision order. A revision at or below the last one applied is
/// skipped, so a batch delivered twice changes nothing. That holds for a revision that changed no
/// tracked field too: it opened no period, but re-applying it matches the open period and is a no-op.
/// </remarks>
public sealed class WorkItemStateTimeline
{
    private readonly Guid _workItemId;
    private readonly Guid _workspaceId;
    private WorkItemStateHistory? _open;
    private int _lastRevision;

    /// <param name="workItemId">The work item.</param>
    /// <param name="workspaceId">The workspace being synced.</param>
    /// <param name="openPeriod">The item's current period, tracked so closing it is saved; null when it has none.</param>
    /// <param name="lastRevision">The highest revision that opened any of the item's periods; 0 when it has none.</param>
    public WorkItemStateTimeline(Guid workItemId, Guid workspaceId, WorkItemStateHistory? openPeriod, int lastRevision)
    {
        _workItemId = workItemId;
        _workspaceId = workspaceId;
        _open = openPeriod;
        _lastRevision = lastRevision;
    }

    /// <summary>
    /// Applies a revision, returning the period it opened, or null when it was already applied or
    /// changed no tracked field.
    /// </summary>
    public WorkItemStateHistory? Apply(int revision, Instant changed, WorkItemTrackedState state)
    {
        if (revision <= _lastRevision)
            return null;

        _lastRevision = revision;

        if (_open is not null && _open.State.HasSameSourceValues(state))
            return null;

        // A revision stamped before the period it follows (clock skew in the source) still has to
        // leave the periods contiguous and ordered, so it starts no earlier than that period.
        var validFrom = _open is not null && changed < _open.ValidFrom ? _open.ValidFrom : changed;

        _open?.Close(validFrom);
        _open = WorkItemStateHistory.Open(_workItemId, _workspaceId, revision, validFrom, state);

        return _open;
    }
}
