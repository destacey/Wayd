using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Scoring;

namespace Wayd.DemandManagement.Domain.Models.Scoring;

/// <summary>
/// An immutable, point-in-time snapshot of an intake request's score under its desk's scoring model. The
/// model's id, key and name, every criterion rating and every computed output are frozen at scoring time, so
/// the record stays meaningful after the model is archived or the request moves to a desk that scores with
/// another. Substance tier.
/// </summary>
/// <remarks>
/// Re-scoring adds a snapshot and overwrites nothing, which is how a request that sits for months shows how
/// its score moved when the facts moved. The current score is the one with the highest
/// <see cref="Sequence"/>.
/// </remarks>
public sealed class IntakeRequestScore : BaseAuditableEntity
{
    private readonly List<IntakeRequestScoreRating> _ratings = [];
    private readonly List<IntakeRequestScoreOutput> _outputs = [];

    private IntakeRequestScore() { }

    private IntakeRequestScore(
        Guid requestId,
        Guid scoringModelId,
        int scoringModelKey,
        string scoringModelName,
        decimal primaryValue,
        Guid scoredById,
        Instant scoredOn,
        long sequence)
    {
        RequestId = requestId;
        ScoringModelId = scoringModelId;
        ScoringModelKey = scoringModelKey;
        ScoringModelName = scoringModelName;
        PrimaryValue = primaryValue;
        ScoredById = scoredById;
        ScoredOn = scoredOn;
        Sequence = sequence;
    }

    /// <summary>
    /// The request this score belongs to.
    /// </summary>
    public Guid RequestId { get; private init; }

    /// <summary>
    /// The scoring model used, frozen at scoring time. A plain reference with no foreign key, because the
    /// snapshot stays valid whatever later happens to the model.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The scoring model's key, frozen at scoring time.
    /// </summary>
    public int ScoringModelKey { get; private init; }

    /// <summary>
    /// The scoring model's name, frozen at scoring time.
    /// </summary>
    public string ScoringModelName
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(ScoringModelName)).Trim();
    } = default!;

    /// <summary>
    /// The value of the model's primary output, which is what requests are ranked on.
    /// </summary>
    public decimal PrimaryValue { get; private init; }

    /// <summary>
    /// When the score was recorded.
    /// </summary>
    public Instant ScoredOn { get; private init; }

    /// <summary>
    /// The agent who rated the request.
    /// </summary>
    public Guid ScoredById { get; private init; }

    /// <summary>
    /// A monotonic per-request number; the highest is the current score. Used instead of
    /// <see cref="ScoredOn"/> so scores recorded in the same instant stay unambiguously ordered.
    /// </summary>
    public long Sequence { get; private init; }

    /// <summary>
    /// The frozen criterion ratings that produced the score, in display order.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestScoreRating> Ratings => _ratings.AsReadOnly();

    /// <summary>
    /// The frozen computed outputs, in evaluation order.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestScoreOutput> Outputs => _outputs.AsReadOnly();

    /// <summary>
    /// Builds a snapshot from a calculated result, copying the criteria and outputs out of the model so
    /// nothing references it afterwards.
    /// </summary>
    /// <param name="model">The model that produced <paramref name="result"/>; read, not retained.</param>
    /// <param name="ratingValuesByCriterionId">The value used for each criterion.</param>
    /// <param name="selectedLevels">For scale-rated criteria, the chosen level's id and label.</param>
    internal static IntakeRequestScore CreateSnapshot(
        Guid requestId,
        ScoringModel model,
        ScoringResult result,
        IReadOnlyDictionary<Guid, decimal> ratingValuesByCriterionId,
        IReadOnlyDictionary<Guid, (Guid LevelId, string Label)>? selectedLevels,
        Guid scoredById,
        Instant scoredOn,
        long sequence)
    {
        var score = new IntakeRequestScore(requestId, model.Id, model.Key, model.Name, result.PrimaryValue,
            scoredById, scoredOn, sequence);

        foreach (var criterion in model.Criteria.OrderBy(c => c.Order))
        {
            Guid? levelId = null;
            string? levelLabel = null;
            if (selectedLevels is not null && selectedLevels.TryGetValue(criterion.Id, out var level))
            {
                levelId = level.LevelId;
                levelLabel = level.Label;
            }

            score._ratings.Add(new IntakeRequestScoreRating(score.Id, criterion.Id, criterion.Name, criterion.Token,
                ratingValuesByCriterionId[criterion.Id], levelId, levelLabel, criterion.Order));
        }

        foreach (var output in model.Outputs.OrderBy(o => o.Order))
        {
            score._outputs.Add(new IntakeRequestScoreOutput(score.Id, output.Token, output.Name,
                result.OutputValues[output.Token], output.IsPrimary, output.Order));
        }

        return score;
    }
}
