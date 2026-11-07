using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Replication;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Demand Management's copy of a PPM project deliverable, sharing its id: what an existing project scope
/// outcome maps to, and what decides whether the request it serves has been met. Kept by the
/// <c>ProjectDeliverable*</c> events and by workflow switches of deliverables (see <see cref="ReplicaWatermark"/>
/// for how it orders them).
/// </summary>
/// <remarks>
/// Holds the status as its category and alias, never its name: a status can be renamed without an event a
/// copy could follow, while its category and alias change only through events. Belongs to its project's
/// <see cref="DemandProject"/> copy and goes with it.
/// </remarks>
public sealed class DemandProjectDeliverable
{
    private DemandProjectDeliverable() { }

    /// <param name="deliverable">The deliverable's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public DemandProjectDeliverable(ISimpleProjectDeliverable deliverable, Instant asOf)
    {
        Id = deliverable.Id;
        ProjectId = deliverable.ProjectId;
        Name = deliverable.Name;
        StatusCategory = deliverable.StatusCategory;
        StatusAlias = deliverable.StatusAlias;
        Watermarks = DemandProjectDeliverableWatermarks.At(asOf);
    }

    /// <summary>The deliverable's id, the same as in PPM.</summary>
    public Guid Id { get; private set; }

    /// <summary>The project the deliverable belongs to. A deliverable never moves between projects.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>What the deliverable is called.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>The category of the deliverable's current status.</summary>
    public StatusCategory StatusCategory { get; private set; }

    /// <summary>The well-known meaning of the deliverable's current status.</summary>
    public ProjectDeliverableStatusAlias StatusAlias { get; private set; }

    /// <summary>Whether the deliverable has been accepted: what an outcome mapped to it waits for.</summary>
    public bool IsAccepted => StatusAlias == ProjectDeliverableStatusAlias.Accepted;

    /// <summary>When each group of fields last took a change.</summary>
    public DemandProjectDeliverableWatermarks Watermarks { get; private set; } = DemandProjectDeliverableWatermarks.None;

    /// <summary>Applies a change to the name made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyName(string name, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Name, timestamp)
            || (Name == name && Watermarks.Name == timestamp))
        {
            return false;
        }

        Name = name;
        Watermarks = Watermarks with { Name = timestamp };
        return true;
    }

    /// <summary>Applies a move to another status made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyStatus(StatusCategory category, ProjectDeliverableStatusAlias alias, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Status, timestamp)
            || (StatusCategory == category && StatusAlias == alias && Watermarks.Status == timestamp))
        {
            return false;
        }

        StatusCategory = category;
        StatusAlias = alias;
        Watermarks = Watermarks with { Status = timestamp };
        return true;
    }

    /// <summary>
    /// Brings the copy in line with the source as read at <paramref name="asOf"/>, one group at a time.
    /// </summary>
    /// <remarks>
    /// A group that already matches keeps its watermark: stamping it would skip a change the read could not
    /// see, one timestamped before <paramref name="asOf"/> but committed after it. A group holding a change
    /// newer than the read is left alone too, because the read is the older of the two.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Resync(ISimpleProjectDeliverable deliverable, Instant asOf)
    {
        if (deliverable.Id != Id)
            throw new InvalidOperationException("Cannot resync a DemandProjectDeliverable with a different Id.");

        var changed = false;

        if (Name != deliverable.Name && !ReplicaWatermark.IsStale(Watermarks.Name, asOf))
        {
            Name = deliverable.Name;
            Watermarks = Watermarks with { Name = asOf };
            changed = true;
        }

        if ((StatusCategory != deliverable.StatusCategory || StatusAlias != deliverable.StatusAlias)
            && !ReplicaWatermark.IsStale(Watermarks.Status, asOf))
        {
            StatusCategory = deliverable.StatusCategory;
            StatusAlias = deliverable.StatusAlias;
            Watermarks = Watermarks with { Status = asOf };
            changed = true;
        }

        return changed;
    }
}
