using NodaTime;

namespace Wayd.Common.Domain.Models.Organizations;

/// <summary>
/// When each group of a team copy's fields last took a change (see <see cref="Replication.ReplicaWatermark"/>).
/// Shared by the Work, Planning and PPM copies of an Organization team.
/// </summary>
/// <param name="Details">The name and code.</param>
/// <param name="Activation">The active flag.</param>
public sealed record TeamReplicaWatermarks(Instant? Details, Instant? Activation)
{
    public static TeamReplicaWatermarks None { get; } = new(null, null);

    public static TeamReplicaWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Details > instant || Activation > instant;
}
