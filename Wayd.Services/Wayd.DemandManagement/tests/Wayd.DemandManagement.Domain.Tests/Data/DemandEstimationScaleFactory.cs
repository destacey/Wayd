using NodaTime;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.DemandManagement.Domain.Models.Estimation;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// Builds Demand Management's copies of estimation scales the way replication does, from a snapshot of the source.
/// </summary>
public static class DemandEstimationScaleFactory
{
    /// <summary>T-shirt sizes and a <c>?</c> card, the values a scale is given unless a test names others.</summary>
    public static readonly EstimationScaleValueValues[] TShirtSizes =
        [new("S", true), new("M", true), new("L", true), new("XL", true), new("?", false)];

    /// <summary>A copy holding one version with <paramref name="values"/>, or <see cref="TShirtSizes"/>.</summary>
    public static DemandEstimationScale Create(string name = "T-Shirt Sizes", bool isActive = true,
        EstimationScaleValueValues[]? values = null, Instant? asOf = null) =>
        new(Snapshot(Guid.NewGuid(), name, isActive, [Version(1, values ?? TShirtSizes)]), asOf ?? Instant.FromUtc(2026, 1, 1, 0, 0));

    /// <summary>The scale as Planning would answer it.</summary>
    public static EstimationScaleSnapshot Snapshot(Guid id, string name, bool isActive, EstimationScaleVersionValues[] versions, int key = 7) =>
        new(id, key, name, isActive, versions);

    /// <summary>A published version with a fresh id.</summary>
    public static EstimationScaleVersionValues Version(int number, EstimationScaleValueValues[]? values = null) =>
        new(Guid.NewGuid(), number, values ?? TShirtSizes);

    /// <summary>Publishes the scale's next version with <paramref name="values"/>, or <see cref="TShirtSizes"/>.</summary>
    public static DemandEstimationScaleVersion PublishNext(this DemandEstimationScale scale, EstimationScaleValueValues[]? values = null)
    {
        var version = Version(scale.CurrentVersion.Number + 1, values);
        scale.AddVersion(version);
        return scale.Versions.Single(v => v.Id == version.VersionId);
    }
}
