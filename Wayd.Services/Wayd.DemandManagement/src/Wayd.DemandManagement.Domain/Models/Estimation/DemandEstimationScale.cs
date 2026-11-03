using NodaTime;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.Common.Domain.Replication;

namespace Wayd.DemandManagement.Domain.Models.Estimation;

/// <summary>
/// Demand Management's copy of a Planning estimation scale, sharing its id: what a desk pins a version of and
/// an estimate is checked against. Kept by the <see cref="IEstimationScaleEvent"/> events (see
/// <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
/// <remarks>
/// Holds only what the module shows and checks: the key, the name, whether it can be chosen, and the versions.
/// </remarks>
public sealed class DemandEstimationScale
{
    private readonly List<DemandEstimationScaleVersion> _versions = [];

    private DemandEstimationScale() { }

    /// <param name="scale">The scale's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public DemandEstimationScale(EstimationScaleSnapshot scale, Instant asOf)
    {
        Id = scale.Id;
        Key = scale.Key;
        Name = scale.Name;
        IsActive = scale.IsActive;
        foreach (var version in scale.Versions)
            AddVersion(version);
        Watermarks = DemandEstimationScaleWatermarks.At(asOf);
    }

    /// <summary>The scale's id, the same as in Planning.</summary>
    public Guid Id { get; private set; }

    /// <summary>The scale's key in Planning.</summary>
    public int Key { get; private set; }

    /// <summary>The scale's name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Whether the scale is active, which a desk choosing it requires.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Every version the scale has published.</summary>
    public IReadOnlyCollection<DemandEstimationScaleVersion> Versions => _versions.AsReadOnly();

    /// <summary>
    /// The latest published version, which a desk newly choosing the scale is pinned to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The versions were not loaded.</exception>
    public DemandEstimationScaleVersion CurrentVersion => _versions.Count > 0
        ? _versions.MaxBy(v => v.Number)!
        : throw new InvalidOperationException($"The versions of estimation scale {Id} were not loaded.");

    /// <summary>When each group of fields last took a change.</summary>
    public DemandEstimationScaleWatermarks Watermarks { get; private set; } = DemandEstimationScaleWatermarks.None;

    /// <summary>
    /// Applies a rename made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyName(string name, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Name, timestamp)
            || (Name == name && Watermarks.Name == timestamp))
        {
            return false;
        }

        Name = name;
        Watermarks = Watermarks with { Name = timestamp };
        return true;
    }

    /// <summary>
    /// Applies an activation or deactivation made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyIsActive(bool isActive, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.IsActive, timestamp)
            || (IsActive == isActive && Watermarks.IsActive == timestamp))
        {
            return false;
        }

        IsActive = isActive;
        Watermarks = Watermarks with { IsActive = timestamp };
        return true;
    }

    /// <summary>
    /// Adds a published version. Needs <see cref="Versions"/> loaded.
    /// </summary>
    /// <returns>False when the copy already holds the version.</returns>
    public bool AddVersion(EstimationScaleVersionValues version)
    {
        if (_versions.Any(v => v.Id == version.VersionId))
            return false;

        _versions.Add(new DemandEstimationScaleVersion(Id, version));
        return true;
    }

    /// <summary>
    /// Brings the copy in line with the source as read at <paramref name="asOf"/>, one group at a time. Needs
    /// <see cref="Versions"/> loaded.
    /// </summary>
    /// <remarks>
    /// A group that already matches keeps its watermark, and a group holding a change newer than the read is
    /// left alone, for the reasons given on <see cref="DemandProject.Resync"/>.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Resync(EstimationScaleSnapshot scale, Instant asOf)
    {
        if (scale.Id != Id)
            throw new InvalidOperationException("Cannot resync a DemandEstimationScale with a different Id.");

        var changed = false;

        if (Name != scale.Name && !ReplicaWatermark.IsStale(Watermarks.Name, asOf))
        {
            Name = scale.Name;
            Watermarks = Watermarks with { Name = asOf };
            changed = true;
        }

        if (IsActive != scale.IsActive && !ReplicaWatermark.IsStale(Watermarks.IsActive, asOf))
        {
            IsActive = scale.IsActive;
            Watermarks = Watermarks with { IsActive = asOf };
            changed = true;
        }

        foreach (var version in scale.Versions)
            changed |= AddVersion(version);

        return changed;
    }
}
