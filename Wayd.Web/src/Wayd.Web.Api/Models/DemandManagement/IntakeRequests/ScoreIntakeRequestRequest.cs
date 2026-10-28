using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// An agent's ratings of an intake request against its desk's scoring model.
/// </summary>
public sealed record ScoreIntakeRequestRequest
{
    /// <summary>A rating for every criterion of the desk's model.</summary>
    public List<IntakeRequestCriterionRatingRequest> Ratings { get; set; } = [];

    /// <summary>The ratings as the application layer takes them.</summary>
    public IReadOnlyList<IntakeRequestCriterionRatingInput> ToInputs() =>
        [.. Ratings.Select(r => new IntakeRequestCriterionRatingInput(r.CriterionId, r.Value, r.RatingLevelId))];
}

/// <summary>
/// One criterion's rating: a level of its scale, or a number for a criterion with no scale.
/// </summary>
public sealed record IntakeRequestCriterionRatingRequest
{
    /// <summary>The criterion rated.</summary>
    public Guid CriterionId { get; set; }

    /// <summary>The number entered, for a criterion with no scale.</summary>
    public decimal? Value { get; set; }

    /// <summary>The level chosen, for a criterion rated on a scale.</summary>
    public Guid? RatingLevelId { get; set; }
}

public sealed class ScoreIntakeRequestRequestValidator : CustomValidator<ScoreIntakeRequestRequest>
{
    public ScoreIntakeRequestRequestValidator()
    {
        RuleFor(r => r.Ratings).NotNull();
        RuleForEach(r => r.Ratings).ChildRules(rating =>
        {
            rating.RuleFor(x => x.CriterionId).NotEmpty();
            rating.RuleFor(x => x.RatingLevelId).NotEqual(Guid.Empty);
        });
    }
}
