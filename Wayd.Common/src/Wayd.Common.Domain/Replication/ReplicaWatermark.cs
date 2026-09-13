using NodaTime;

namespace Wayd.Common.Domain.Replication;

/// <summary>
/// The ordering rule a module's copy of another module's record uses to apply events that arrive late, twice,
/// or out of order. Each group of fields that one kind of change writes keeps the timestamp of the last change
/// applied to it, and a change older than that is skipped because a newer one already replaced it.
/// </summary>
/// <remarks>
/// A single watermark for the whole record is not enough: a rename applied at 10:02 would make a deactivation
/// from 10:01 look stale, although nothing newer ever wrote the active flag. See "Consuming an event" in
/// docs/contributing/domain-events.mdx.
/// </remarks>
public static class ReplicaWatermark
{
    /// <summary>
    /// Whether a change timestamped <paramref name="timestamp"/> is older than the last change applied to the
    /// group. A tie is not stale: a redelivery carries the same timestamp and must re-apply cleanly.
    /// </summary>
    public static bool IsStale(Instant? watermark, Instant timestamp) =>
        watermark is { } applied && timestamp < applied;
}
