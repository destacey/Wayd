using Wayd.Common.Domain.Events.Planning.EstimationScales;

namespace Wayd.DemandManagement.Domain.Models.Estimation;

/// <summary>
/// Demand Management's copy of one published estimation scale version, sharing its id. A version never changes
/// once published, so the copy is written once and never updated.
/// </summary>
public sealed class DemandEstimationScaleVersion
{
    private readonly List<DemandEstimationScaleValue> _values = [];

    private DemandEstimationScaleVersion() { }

    internal DemandEstimationScaleVersion(Guid scaleId, EstimationScaleVersionValues version)
    {
        Id = version.VersionId;
        ScaleId = scaleId;
        Number = version.Number;
        _values.AddRange(version.Values.Select(v => new DemandEstimationScaleValue(v.Value, v.IsEstimate)));
    }

    /// <summary>The version's id, the same as in Planning.</summary>
    public Guid Id { get; private set; }

    /// <summary>The scale the version belongs to.</summary>
    public Guid ScaleId { get; private set; }

    /// <summary>The version's number within its scale, starting at 1.</summary>
    public int Number { get; private set; }

    /// <summary>The version's values, cards included, in display order.</summary>
    public IReadOnlyList<DemandEstimationScaleValue> Values => _values.AsReadOnly();

    /// <summary>
    /// The estimate value on this version whose text is exactly <paramref name="value"/> once trimmed, with its
    /// position among the version's values, or null when there is none or it is a card.
    /// </summary>
    public (DemandEstimationScaleValue Value, int Position)? FindEstimate(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var position = _values.FindIndex(v => v.IsEstimate && string.Equals(v.Value, trimmed, StringComparison.Ordinal));
        return position < 0 ? null : (_values[position], position);
    }
}
