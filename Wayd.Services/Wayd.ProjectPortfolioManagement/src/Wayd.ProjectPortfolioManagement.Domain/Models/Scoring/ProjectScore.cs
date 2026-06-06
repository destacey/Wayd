using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Scoring;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

/// <summary>
/// An immutable, point-in-time snapshot of a project's score under a scoring model. The model id and
/// name, every criterion rating, and every computed output value are frozen at scoring time, so the
/// record stays meaningful even after the source <see cref="ScoringModel"/> is archived or replaced.
/// Each re-score creates a new <see cref="ProjectScore"/>; the project's current score is the one with
/// the highest <see cref="Sequence"/>.
/// </summary>
public sealed class ProjectScore : BaseAuditableEntity
{
    private readonly List<ProjectScoreRating> _ratings = [];
    private readonly List<ProjectScoreOutput> _outputs = [];

    private ProjectScore() { }

    private ProjectScore(
        Guid projectId,
        Guid scoringModelId,
        int scoringModelKey,
        string scoringModelName,
        decimal primaryValue,
        Guid scoredById,
        Instant scoredOn,
        long sequence)
    {
        ProjectId = projectId;
        ScoringModelId = scoringModelId;
        ScoringModelKey = scoringModelKey;
        ScoringModelName = scoringModelName;
        PrimaryValue = primaryValue;
        ScoredById = scoredById;
        ScoredOn = scoredOn;
        Sequence = sequence;
    }

    /// <summary>
    /// The ID of the project this score belongs to.
    /// </summary>
    public Guid ProjectId { get; private init; }

    /// <summary>
    /// The ID of the scoring model used, frozen at scoring time. This is a plain reference with no
    /// foreign key — the model may later be archived, but this snapshot remains valid.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The scoring model's human-readable key, frozen at scoring time.
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
    /// The primary score value computed by the model's primary output.
    /// </summary>
    public decimal PrimaryValue { get; private init; }

    /// <summary>
    /// The instant the score was recorded.
    /// </summary>
    public Instant ScoredOn { get; private init; }

    /// <summary>
    /// The ID of the employee who recorded the score.
    /// </summary>
    public Guid ScoredById { get; private init; }

    /// <summary>
    /// The employee who recorded the score.
    /// </summary>
    public Employee? ScoredBy { get; private set; }

    /// <summary>
    /// A monotonic per-project sequence number. The highest sequence is the project's current score.
    /// Used instead of <see cref="ScoredOn"/> so that re-scores recorded in the same instant remain
    /// unambiguously ordered.
    /// </summary>
    public long Sequence { get; private init; }

    /// <summary>
    /// The frozen criterion ratings that produced this score, in display order.
    /// </summary>
    public IReadOnlyCollection<ProjectScoreRating> Ratings => _ratings.AsReadOnly();

    /// <summary>
    /// The frozen computed output values for this score, in evaluation order.
    /// </summary>
    public IReadOnlyCollection<ProjectScoreOutput> Outputs => _outputs.AsReadOnly();

    /// <summary>
    /// Builds an immutable score snapshot from a calculated result. The criterion ratings and output
    /// values are copied from the supplied model and result; nothing references the model after this.
    /// </summary>
    /// <param name="projectId">The project being scored.</param>
    /// <param name="model">The model that produced <paramref name="result"/> (read-only; not retained).</param>
    /// <param name="result">The computed scoring result.</param>
    /// <param name="ratingValuesByCriterionId">The rating value used for each criterion.</param>
    /// <param name="selectedLevels">The selected (level id, label) per criterion for scale-based criteria.</param>
    /// <param name="scoredById">The employee recording the score.</param>
    /// <param name="scoredOn">When the score was recorded.</param>
    /// <param name="sequence">The monotonic per-project sequence number.</param>
    internal static ProjectScore CreateSnapshot(
        Guid projectId,
        ScoringModel model,
        ScoringResult result,
        IReadOnlyDictionary<Guid, decimal> ratingValuesByCriterionId,
        IReadOnlyDictionary<Guid, (Guid LevelId, string Label)>? selectedLevels,
        Guid scoredById,
        Instant scoredOn,
        long sequence)
    {
        var score = new ProjectScore(
            projectId,
            model.Id,
            model.Key,
            model.Name,
            result.PrimaryValue,
            scoredById,
            scoredOn,
            sequence);

        foreach (var criterion in model.Criteria.OrderBy(c => c.Order))
        {
            var value = ratingValuesByCriterionId[criterion.Id];
            Guid? levelId = null;
            string? levelLabel = null;

            if (selectedLevels is not null && selectedLevels.TryGetValue(criterion.Id, out var level))
            {
                levelId = level.LevelId;
                levelLabel = level.Label;
            }

            score._ratings.Add(new ProjectScoreRating(
                score.Id,
                criterion.Id,
                criterion.Name,
                criterion.Token,
                value,
                levelId,
                levelLabel,
                criterion.Order));
        }

        foreach (var output in model.Outputs.OrderBy(o => o.Order))
        {
            score._outputs.Add(new ProjectScoreOutput(
                score.Id,
                output.Token,
                output.Name,
                result.OutputValues[output.Token],
                output.IsPrimary,
                output.Order));
        }

        return score;
    }
}
