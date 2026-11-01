using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// One value on an estimation scale version: what a participant can vote, and whether it can become an
/// estimate. A value has no identity of its own — it is defined entirely by its text and flag.
/// </summary>
public sealed class EstimationScaleValue : ValueObject
{
    /// <summary>
    /// The longest a value may be, in characters.
    /// </summary>
    public const int MaxLength = 32;

    private EstimationScaleValue() { }

    public EstimationScaleValue(string value, bool isEstimate)
    {
        Value = value;
        IsEstimate = isEstimate;
    }

    /// <summary>
    /// The text shown on the card and recorded as the vote, such as <c>5</c>, <c>M</c> or <c>?</c>. Unique
    /// within its version, compared exactly. At most <see cref="MaxLength"/> characters once trimmed.
    /// </summary>
    public string Value
    {
        get;
        private set
        {
            var trimmed = Guard.Against.NullOrWhiteSpace(value, nameof(Value)).Trim();
            field = trimmed.Length <= MaxLength
                ? trimmed
                : throw new ArgumentException($"An estimation scale value can be at most {MaxLength} characters.", nameof(Value));
        }
    } = default!;

    /// <summary>
    /// Whether the value is a size. False for a card such as <c>?</c>, <c>∞</c> or <c>☕</c>, which can be voted
    /// but never becomes an estimate.
    /// </summary>
    public bool IsEstimate { get; private set; }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Value;
        yield return IsEstimate;
    }
}
