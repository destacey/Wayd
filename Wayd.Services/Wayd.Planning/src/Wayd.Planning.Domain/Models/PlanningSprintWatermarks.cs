using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

/// <summary>
/// When each group of a <see cref="PlanningSprint"/>'s fields last took a change (see <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Details">The name and type.</param>
/// <param name="DateRange">The start and end.</param>
/// <param name="Team">The owning team.</param>
public sealed record PlanningSprintWatermarks(Instant? Details, Instant? DateRange, Instant? Team)
{
    public static PlanningSprintWatermarks None { get; } = new(null, null, null);

    public static PlanningSprintWatermarks At(Instant timestamp) => new(timestamp, timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) =>
        Details > instant || DateRange > instant || Team > instant;
}
