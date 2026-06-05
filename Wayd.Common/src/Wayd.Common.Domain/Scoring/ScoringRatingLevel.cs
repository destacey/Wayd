using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Represents a single rating level on a <see cref="ScoringScale"/> (e.g., "Very High" = 5, "Low" = 2).
/// The numeric <see cref="Value"/> is what a chosen rating contributes to a criterion's value; the
/// <see cref="Label"/> is shown in the rating dropdown.
/// </summary>
public sealed class ScoringRatingLevel : BaseAuditableEntity
{
    private ScoringRatingLevel() { }

    internal ScoringRatingLevel(Guid scoringScaleId, string label, decimal value, int order)
    {
        ScoringScaleId = scoringScaleId;
        Label = label;
        Value = value;
        Order = order;
    }

    /// <summary>
    /// The ID of the scale this rating level belongs to.
    /// </summary>
    public Guid ScoringScaleId { get; private init; }

    /// <summary>
    /// The human-readable label for this rating level (e.g., "Very High", "Medium").
    /// </summary>
    public string Label
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Label)).Trim();
    } = default!;

    /// <summary>
    /// The numeric value used in the weighted score calculation.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>
    /// The display order of the rating level within the scale.
    /// </summary>
    public int Order { get; internal set; }

    /// <summary>
    /// Updates the rating level details.
    /// </summary>
    internal Result Update(string label, decimal value)
    {
        Label = label;
        Value = value;
        return Result.Success();
    }
}
