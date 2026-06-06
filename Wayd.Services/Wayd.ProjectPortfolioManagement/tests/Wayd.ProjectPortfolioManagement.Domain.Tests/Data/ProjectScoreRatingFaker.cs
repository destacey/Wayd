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
    public static ProjectScoreRatingFaker WithData(
        this ProjectScoreRatingFaker faker,
        Guid? id = null,
        Guid? projectScoreId = null,
        Guid? criterionId = null,
        string? criterionName = null,
        string? criterionToken = null,
        decimal? ratingValue = null,
        Guid? ratingLevelId = null,
        string? ratingLevelLabel = null,
        int? order = null)
    {
        if (id.HasValue) { faker.RuleFor(x => x.Id, id.Value); }
        if (projectScoreId.HasValue) { faker.RuleFor(x => x.ProjectScoreId, projectScoreId.Value); }
        if (criterionId.HasValue) { faker.RuleFor(x => x.CriterionId, criterionId.Value); }
        if (!string.IsNullOrWhiteSpace(criterionName)) { faker.RuleFor(x => x.CriterionName, criterionName); }
        if (!string.IsNullOrWhiteSpace(criterionToken)) { faker.RuleFor(x => x.CriterionToken, criterionToken); }
        if (ratingValue.HasValue) { faker.RuleFor(x => x.RatingValue, ratingValue.Value); }
        if (ratingLevelId.HasValue) { faker.RuleFor(x => x.RatingLevelId, ratingLevelId.Value); }
        if (!string.IsNullOrWhiteSpace(ratingLevelLabel)) { faker.RuleFor(x => x.RatingLevelLabel, ratingLevelLabel); }
        if (order.HasValue) { faker.RuleFor(x => x.Order, order.Value); }

        return faker;
    }
}
