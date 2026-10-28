using NodaTime;

namespace Wayd.DemandManagement.Domain.Models.Scoring;

/// <summary>
/// The request's latest score, kept on <see cref="IntakeRequest"/> so the requests list and board can sort on
/// it without reading the score history. Substance tier.
/// </summary>
/// <remarks>
/// A cache of the highest-sequence <see cref="IntakeRequestScore"/>, written only by
/// <see cref="IntakeRequest.RecordScore"/>, so the two cannot disagree. <see cref="ScoringModelId"/> is what
/// lets a ranking count only scores recorded with the desk's current model.
/// </remarks>
public sealed class IntakeRequestScoreSummary
{
    private IntakeRequestScoreSummary() { }

    internal IntakeRequestScoreSummary(decimal value, Instant scoredOn, Guid scoredById, Guid scoringModelId, string scoringModelName)
    {
        Value = value;
        ScoredOn = scoredOn;
        ScoredById = scoredById;
        ScoringModelId = scoringModelId;
        ScoringModelName = scoringModelName;
    }

    /// <summary>
    /// The primary value of the latest score.
    /// </summary>
    public decimal Value { get; private init; }

    /// <summary>
    /// When the latest score was recorded.
    /// </summary>
    public Instant ScoredOn { get; private init; }

    /// <summary>
    /// The agent who recorded the latest score.
    /// </summary>
    public Guid ScoredById { get; private init; }

    /// <summary>
    /// The model that produced the latest score.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The name of the model that produced the latest score, frozen at scoring time.
    /// </summary>
    public string ScoringModelName { get; private init; } = default!;
}
