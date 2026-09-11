using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio's scoring model was assigned, replaced, or removed.
/// </summary>
/// <remarks>
/// One event rather than a type per verb: what the portfolio scores against is a state, and assigning,
/// swapping and clearing all leave it in one. A null <see cref="ScoringModelId"/> is the cleared state,
/// which stops new scoring.
/// <para>
/// The model is named as well as identified, so an entry stays readable after the model is renamed or
/// retired. Scores already recorded are unaffected either way — each froze the model it was scored
/// against — so this describes what happens next, not a change to any existing score.
/// </para>
/// <para>
/// Carries both ends, each identified and named, so a swap reads as "moved from one model to another" on
/// its own — and the model it replaced is the one most likely to have been renamed or retired by the time
/// anyone reads the entry.
/// </para>
/// </remarks>
public sealed record ProjectPortfolioScoringModelChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioScoringModelChangedEvent(
        Guid id,
        int key,
        Guid? previousScoringModelId,
        string? previousScoringModelName,
        Guid? scoringModelId,
        string? scoringModelName,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousScoringModelId = previousScoringModelId;
        PreviousScoringModelName = previousScoringModelName;
        ScoringModelId = scoringModelId;
        ScoringModelName = scoringModelName;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The model the portfolio scored against before the change, or null when it had none.</summary>
    public Guid? PreviousScoringModelId { get; }

    public string? PreviousScoringModelName { get; }

    /// <summary>The model the portfolio scores against after the change, or null once cleared.</summary>
    public Guid? ScoringModelId { get; }

    public string? ScoringModelName { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
