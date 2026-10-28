using NodaTime;
using Wayd.Common.Domain.Replication;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// When each group of a <see cref="DemandProject"/>'s fields last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Key">The key.</param>
public sealed record DemandProjectWatermarks(Instant? Name, Instant? Key)
{
    /// <summary>No group has taken a change yet.</summary>
    public static DemandProjectWatermarks None { get; } = new(null, null);

    /// <summary>Every group stamped at <paramref name="timestamp"/>, for a copy made whole from its source.</summary>
    public static DemandProjectWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Name > instant || Key > instant;
}
