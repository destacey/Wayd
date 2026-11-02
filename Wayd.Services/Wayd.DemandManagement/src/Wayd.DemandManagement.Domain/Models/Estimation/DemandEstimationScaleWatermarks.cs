using NodaTime;
using Wayd.Common.Domain.Replication;

namespace Wayd.DemandManagement.Domain.Models.Estimation;

/// <summary>
/// When each group of a <see cref="DemandEstimationScale"/>'s fields last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <remarks>
/// Versions have no watermark: they are published, never changed, so adding one twice or late is harmless.
/// </remarks>
/// <param name="Name">The name.</param>
/// <param name="IsActive">Whether the scale is active.</param>
public sealed record DemandEstimationScaleWatermarks(Instant? Name, Instant? IsActive)
{
    /// <summary>No group has taken a change yet.</summary>
    public static DemandEstimationScaleWatermarks None { get; } = new(null, null);

    /// <summary>Every group stamped at <paramref name="timestamp"/>, for a copy made whole from its source.</summary>
    public static DemandEstimationScaleWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Name > instant || IsActive > instant;
}
