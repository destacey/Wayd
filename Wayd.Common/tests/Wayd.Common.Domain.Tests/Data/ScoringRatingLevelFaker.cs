using Wayd.Common.Domain.Scoring;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ScoringRatingLevelFaker : PrivateConstructorFaker<ScoringRatingLevel>
{
    public ScoringRatingLevelFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ScoringScaleId, f => f.Random.Guid());
        RuleFor(x => x.Label, f => f.Lorem.Word());
        RuleFor(x => x.Value, f => f.Random.Decimal(1m, 5m));
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
    }
}

public static class ScoringRatingLevelFakerExtensions
{
    public static ScoringRatingLevelFaker WithId(this ScoringRatingLevelFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ScoringRatingLevelFaker WithScoringScaleId(this ScoringRatingLevelFaker faker, Guid scoringScaleId)
    {
        faker.RuleFor(x => x.ScoringScaleId, scoringScaleId);
        return faker;
    }

    public static ScoringRatingLevelFaker WithLabel(this ScoringRatingLevelFaker faker, string label)
    {
        faker.RuleFor(x => x.Label, label);
        return faker;
    }

    public static ScoringRatingLevelFaker WithValue(this ScoringRatingLevelFaker faker, decimal value)
    {
        faker.RuleFor(x => x.Value, value);
        return faker;
    }

    public static ScoringRatingLevelFaker WithOrder(this ScoringRatingLevelFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
