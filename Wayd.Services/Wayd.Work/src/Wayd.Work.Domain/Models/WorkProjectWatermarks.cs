using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// When each group of a <see cref="WorkProject"/>'s fields last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Details">The name and description.</param>
/// <param name="Key">The key.</param>
public sealed record WorkProjectWatermarks(Instant? Details, Instant? Key)
{
    public static WorkProjectWatermarks None { get; } = new(null, null);

    public static WorkProjectWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Details > instant || Key > instant;
}
