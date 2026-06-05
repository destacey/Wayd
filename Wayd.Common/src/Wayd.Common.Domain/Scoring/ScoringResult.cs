namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// The outcome of evaluating a scoring model against a set of rated criteria: the primary score plus
/// every computed output value (keyed by output token), so intermediate values such as Cost of Delay
/// can be displayed and ranked alongside the score.
/// </summary>
public sealed record ScoringResult(decimal PrimaryValue, IReadOnlyDictionary<string, decimal> OutputValues);
