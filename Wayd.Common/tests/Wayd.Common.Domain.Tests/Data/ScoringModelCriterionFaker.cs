using Wayd.Common.Domain.Scoring;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ScoringModelCriterionFaker : PrivateConstructorFaker<ScoringModelCriterion>
{
    public ScoringModelCriterionFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ScoringModelId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.Department());
        RuleFor(x => x.Token, f => "C" + f.Random.AlphaNumeric(5));
        RuleFor(x => x.Description, f => f.Random.Bool() ? f.Lorem.Sentence() : null);
        RuleFor(x => x.Weight, f => f.Random.Decimal(0m, 100m));
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
    }
}

public static class ScoringModelCriterionFakerExtensions
{
    public static ScoringModelCriterionFaker WithId(this ScoringModelCriterionFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ScoringModelCriterionFaker WithScoringModelId(this ScoringModelCriterionFaker faker, Guid scoringModelId)
    {
        faker.RuleFor(x => x.ScoringModelId, scoringModelId);
        return faker;
    }

    public static ScoringModelCriterionFaker WithName(this ScoringModelCriterionFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static ScoringModelCriterionFaker WithToken(this ScoringModelCriterionFaker faker, string token)
    {
        faker.RuleFor(x => x.Token, token);
        return faker;
    }

    public static ScoringModelCriterionFaker WithDescription(this ScoringModelCriterionFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static ScoringModelCriterionFaker WithWeight(this ScoringModelCriterionFaker faker, decimal? weight)
    {
        faker.RuleFor(x => x.Weight, weight);
        return faker;
    }

    public static ScoringModelCriterionFaker WithScaleId(this ScoringModelCriterionFaker faker, Guid? scaleId)
    {
        faker.RuleFor(x => x.ScaleId, scaleId);
        return faker;
    }

    public static ScoringModelCriterionFaker WithOrder(this ScoringModelCriterionFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
