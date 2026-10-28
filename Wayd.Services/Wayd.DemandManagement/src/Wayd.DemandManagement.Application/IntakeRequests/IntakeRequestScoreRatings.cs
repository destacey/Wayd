using Wayd.Common.Domain.Scoring;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// One criterion's rating as an agent enters it: a level of the criterion's scale, or a number for a criterion
/// with no scale.
/// </summary>
/// <param name="CriterionId">The criterion rated.</param>
/// <param name="Value">The number entered, for a criterion with no scale.</param>
/// <param name="RatingLevelId">The level chosen, for a criterion rated on a scale.</param>
public sealed record IntakeRequestCriterionRatingInput(Guid CriterionId, decimal? Value, Guid? RatingLevelId);

/// <summary>
/// Turns the ratings an agent entered into the values a scoring model calculates from.
/// </summary>
internal static class IntakeRequestScoreRatings
{
    /// <summary>
    /// The largest number a rating can hold, as a score stores it: four decimal places, five digits before.
    /// </summary>
    public const decimal MaxRatingValue = 99_999.9999m;

    /// <summary>
    /// The largest value a computed output can hold, as a score stores it: four decimal places, fourteen digits
    /// before.
    /// </summary>
    public const decimal MaxOutputValue = 99_999_999_999_999.9999m;

    /// <summary>
    /// The value for each of the model's criteria, and the level chosen for each scale-rated one. Refused when a
    /// criterion is unrated, a scale-rated one has no level from its scale, or another has no number. Needs the
    /// model's criteria and scales with their levels loaded.
    /// </summary>
    public static Result<(IReadOnlyDictionary<Guid, decimal> Values, IReadOnlyDictionary<Guid, (Guid LevelId, string Label)> Levels)> Resolve(
        ScoringModel model,
        IReadOnlyCollection<IntakeRequestCriterionRatingInput> ratings)
    {
        var byCriterionId = ratings.GroupBy(r => r.CriterionId).ToDictionary(g => g.Key, g => g.Last());
        var values = new Dictionary<Guid, decimal>();
        var levels = new Dictionary<Guid, (Guid LevelId, string Label)>();

        foreach (var criterion in model.Criteria)
        {
            if (!byCriterionId.TryGetValue(criterion.Id, out var rating))
                return Fail($"Criterion '{criterion.Name}' has not been rated.");

            if (criterion.ScaleId is { } scaleId)
            {
                var level = rating.RatingLevelId is { } levelId
                    ? model.Scales.FirstOrDefault(s => s.Id == scaleId)?.Levels.FirstOrDefault(l => l.Id == levelId)
                    : null;
                if (level is null)
                    return Fail($"Rate criterion '{criterion.Name}' by choosing a level from its scale.");

                values[criterion.Id] = level.Value;
                levels[criterion.Id] = (level.Id, level.Label);
            }
            else
            {
                if (rating.Value is not { } value)
                    return Fail($"Rate criterion '{criterion.Name}' with a number.");

                if (Math.Abs(value) > MaxRatingValue || decimal.Round(value, 4) != value)
                    return Fail($"Rate criterion '{criterion.Name}' with a number between -{MaxRatingValue:0.####} and {MaxRatingValue:0.####}, to at most four decimal places.");

                values[criterion.Id] = value;
            }
        }

        return Result.Success<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>((values, levels));
    }

    private static Result<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)> Fail(string error) =>
        Result.Failure<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>(error);

    /// <summary>
    /// The model with what scoring reads from it: criteria, scales with their levels, and outputs. Untracked, as
    /// the module never changes a model.
    /// </summary>
    public static Task<ScoringModel?> FindForScoring(this IDemandManagementDbContext dbContext, Guid id,
        CancellationToken cancellationToken) =>
        dbContext.ScoringModels
            .AsSplitQuery()
            .Include(m => m.Criteria)
            .Include(m => m.Scales).ThenInclude(s => s.Levels)
            .Include(m => m.Outputs)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}
