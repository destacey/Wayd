using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PokerSessionFaker : PrivateConstructorFaker<PokerSession>
{
    public PokerSessionFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.EstimationScaleId, f => f.Random.Int(1, 100));
        RuleFor(x => x.FacilitatorId, f => f.Random.Guid().ToString());
        RuleFor(x => x.Status, PokerSessionStatus.Active);
    }
}

public static class PokerSessionFakerExtensions
{
    public static PokerSessionFaker WithId(this PokerSessionFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PokerSessionFaker WithKey(this PokerSessionFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static PokerSessionFaker WithName(this PokerSessionFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PokerSessionFaker WithEstimationScaleId(this PokerSessionFaker faker, int estimationScaleId)
    {
        faker.RuleFor(x => x.EstimationScaleId, estimationScaleId);
        return faker;
    }

    public static PokerSessionFaker WithFacilitatorId(this PokerSessionFaker faker, string facilitatorId)
    {
        faker.RuleFor(x => x.FacilitatorId, facilitatorId);
        return faker;
    }

    public static PokerSessionFaker WithStatus(this PokerSessionFaker faker, PokerSessionStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }
}
