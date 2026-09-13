using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// When each group of a <see cref="WorkIteration"/>'s fields last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Details">The name and type.</param>
/// <param name="DateRange">The start and end.</param>
/// <param name="State">Future, Active or Completed.</param>
/// <param name="Team">The owning team.</param>
public sealed record WorkIterationWatermarks(Instant? Details, Instant? DateRange, Instant? State, Instant? Team)
{
    public static WorkIterationWatermarks None { get; } = new(null, null, null, null);

    public static WorkIterationWatermarks At(Instant timestamp) => new(timestamp, timestamp, timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) =>
        Details > instant || DateRange > instant || State > instant || Team > instant;
}
