using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectScoreRatingFaker : PrivateConstructorFaker<ProjectScoreRating>
{
    public ProjectScoreRatingFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ProjectScoreId, f => f.Random.Guid());
        RuleFor(x => x.CriterionId, f => f.Random.Guid());
        RuleFor(x => x.CriterionName, f => f.Commerce.ProductName());
        RuleFor(x => x.CriterionToken, f => f.Random.AlphaNumeric(3));
        RuleFor(x => x.RatingValue, f => f.Random.Decimal(1, 10));
        RuleFor(x => x.RatingLevelId, f => null);
        RuleFor(x => x.RatingLevelLabel, f => null);
        RuleFor(x => x.Order, f => 1);
    }
}

public static class ProjectScoreRatingFakerExtensions
{
    public static ProjectScoreRatingFaker WithId(this ProjectScoreRatingFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectScoreRatingFaker WithProjectScoreId(this ProjectScoreRatingFaker faker, Guid? projectScoreId)
    {
        faker.RuleFor(x => x.ProjectScoreId, projectScoreId);

        return faker;
    }

    public static ProjectScoreRatingFaker WithCriterionId(this ProjectScoreRatingFaker faker, Guid? criterionId)
    {
        faker.RuleFor(x => x.CriterionId, criterionId);

        return faker;
    }

    public static ProjectScoreRatingFaker WithCriterionName(this ProjectScoreRatingFaker faker, string? criterionName)
    {
        faker.RuleFor(x => x.CriterionName, criterionName);

        return faker;
    }

    public static ProjectScoreRatingFaker WithCriterionToken(this ProjectScoreRatingFaker faker, string? criterionToken)
    {
        faker.RuleFor(x => x.CriterionToken, criterionToken);

        return faker;
    }

    public static ProjectScoreRatingFaker WithRatingValue(this ProjectScoreRatingFaker faker, decimal? ratingValue)
    {
        faker.RuleFor(x => x.RatingValue, ratingValue);

        return faker;
    }

    public static ProjectScoreRatingFaker WithRatingLevelId(this ProjectScoreRatingFaker faker, Guid? ratingLevelId)
    {
        faker.RuleFor(x => x.RatingLevelId, ratingLevelId);

        return faker;
    }

    public static ProjectScoreRatingFaker WithRatingLevelLabel(this ProjectScoreRatingFaker faker, string? ratingLevelLabel)
    {
        faker.RuleFor(x => x.RatingLevelLabel, ratingLevelLabel);

        return faker;
    }

    public static ProjectScoreRatingFaker WithOrder(this ProjectScoreRatingFaker faker, int? order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }
}