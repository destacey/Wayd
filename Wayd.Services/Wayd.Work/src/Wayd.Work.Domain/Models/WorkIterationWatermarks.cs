using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// When a <see cref="WorkIteration"/> last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Record">
/// Every field. The iteration events write the whole record at once, so the copy has a single group.
/// </param>
public sealed record WorkIterationWatermarks(Instant? Record)
{
    public static WorkIterationWatermarks None { get; } = new((Instant?)null);

    public static WorkIterationWatermarks At(Instant timestamp) => new(timestamp);

    /// <summary>Whether the copy took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Record > instant;
}
