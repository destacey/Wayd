using NodaTime;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// One published set of an estimation scale's values. A version never changes once published: changing a
/// scale's values publishes the next version, so a vote, a consensus or an estimate recorded against a
/// version keeps meaning what it meant.
/// </summary>
public sealed class EstimationScaleVersion : BaseEntity
{
    private readonly List<EstimationScaleValue> _values = [];

    private EstimationScaleVersion() { }

    internal EstimationScaleVersion(Guid estimationScaleId, int number, IEnumerable<EstimationScaleValue> values, Instant publishedOn)
    {
        EstimationScaleId = estimationScaleId;
        Number = number;
        _values.AddRange(values);
        PublishedOn = publishedOn;
    }

    /// <summary>
    /// The scale this version belongs to.
    /// </summary>
    public Guid EstimationScaleId { get; private init; }

    /// <summary>
    /// The version's number within its scale: 1 for the values the scale was created with, then one more for
    /// each change to them.
    /// </summary>
    public int Number { get; private init; }

    /// <summary>
    /// When the version was published.
    /// </summary>
    public Instant PublishedOn { get; private init; }

    /// <summary>
    /// The values a participant can vote, in display order.
    /// </summary>
    public IReadOnlyList<EstimationScaleValue> Values => _values.AsReadOnly();

    /// <summary>
    /// The value on this version whose text is exactly <paramref name="value"/> once trimmed, or null.
    /// </summary>
    public EstimationScaleValue? Find(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : _values.FirstOrDefault(v => string.Equals(v.Value, trimmed, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether this version holds the same values, flags and order as <paramref name="values"/>.
    /// </summary>
    internal bool HasValues(IReadOnlyList<EstimationScaleValue> values) => _values.SequenceEqual(values);
}
