using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// When each group of a <see cref="StrategicTheme"/> copy's fields last took a change (see
/// <see cref="ReplicaWatermark"/>).
/// </summary>
/// <param name="Details">The name and description.</param>
/// <param name="State">The lifecycle state.</param>
public sealed record StrategicThemeWatermarks(Instant? Details, Instant? State)
{
    public static StrategicThemeWatermarks None { get; } = new(null, null);

    public static StrategicThemeWatermarks At(Instant timestamp) => new(timestamp, timestamp);

    /// <summary>Whether any group took a change later than <paramref name="instant"/>.</summary>
    public bool AnyAfter(Instant instant) => Details > instant || State > instant;
}
