using Wayd.Common.Domain.Scoring;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ScoringScaleFaker : PrivateConstructorFaker<ScoringScale>
{
    public ScoringScaleFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ScoringModelId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
    }
}

public static class ScoringScaleFakerExtensions
{
    public static ScoringScaleFaker WithId(this ScoringScaleFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ScoringScaleFaker WithScoringModelId(this ScoringScaleFaker faker, Guid scoringModelId)
    {
        faker.RuleFor(x => x.ScoringModelId, scoringModelId);
        return faker;
    }

    public static ScoringScaleFaker WithName(this ScoringScaleFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static ScoringScaleFaker WithOrder(this ScoringScaleFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
