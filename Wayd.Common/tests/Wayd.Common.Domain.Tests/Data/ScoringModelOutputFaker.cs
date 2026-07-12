using Wayd.Common.Domain.Scoring;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class ScoringModelOutputFaker : PrivateConstructorFaker<ScoringModelOutput>
{
    public ScoringModelOutputFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ScoringModelId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Token, f => "O" + f.Random.AlphaNumeric(5));
        RuleFor(x => x.Formula, f => "1 + 1");
        RuleFor(x => x.IsPrimary, true);
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
    }
}

public static class ScoringModelOutputFakerExtensions
{
    public static ScoringModelOutputFaker WithId(this ScoringModelOutputFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ScoringModelOutputFaker WithScoringModelId(this ScoringModelOutputFaker faker, Guid scoringModelId)
    {
        faker.RuleFor(x => x.ScoringModelId, scoringModelId);
        return faker;
    }

    public static ScoringModelOutputFaker WithName(this ScoringModelOutputFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static ScoringModelOutputFaker WithToken(this ScoringModelOutputFaker faker, string token)
    {
        faker.RuleFor(x => x.Token, token);
        return faker;
    }

    public static ScoringModelOutputFaker WithFormula(this ScoringModelOutputFaker faker, string formula)
    {
        faker.RuleFor(x => x.Formula, formula);
        return faker;
    }

    public static ScoringModelOutputFaker WithIsPrimary(this ScoringModelOutputFaker faker, bool isPrimary)
    {
        faker.RuleFor(x => x.IsPrimary, isPrimary);
        return faker;
    }

    public static ScoringModelOutputFaker WithOrder(this ScoringModelOutputFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
