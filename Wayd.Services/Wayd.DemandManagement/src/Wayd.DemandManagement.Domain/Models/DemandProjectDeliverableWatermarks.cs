using NodaTime;
using Wayd.Common.Domain.Replication;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// When each group of a <see cref="DemandProjectDeliverable"/>'s fields last took a change (see
/// <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Status">The status category and alias, which always change together.</param>
public sealed record DemandProjectDeliverableWatermarks(Instant? Name, Instant? Status)
{
    /// <summary>No group has taken a change yet.</summary>
    public static DemandProjectDeliverableWatermarks None { get; } = new(null, null);

    /// <summary>Every group stamped at <paramref name="timestamp"/>, for a copy made whole from its source.</summary>
    public static DemandProjectDeliverableWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Name > instant || Status > instant;
}
