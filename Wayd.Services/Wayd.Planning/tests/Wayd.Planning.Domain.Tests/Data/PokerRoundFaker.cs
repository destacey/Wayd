using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class PokerRoundFaker : PrivateConstructorFaker<PokerRound>
{
    public PokerRoundFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.PokerSessionId, f => f.Random.Guid());
        RuleFor(x => x.Label, f => $"WI-{f.Random.Int(1, 999)}: {f.Lorem.Sentence(3)}");
        RuleFor(x => x.Status, PokerRoundStatus.Voting);
        RuleFor(x => x.Order, f => f.Random.Int(0, 20));
    }
}

public static class PokerRoundFakerExtensions
{
    public static PokerRoundFaker WithId(this PokerRoundFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PokerRoundFaker WithPokerSessionId(this PokerRoundFaker faker, Guid sessionId)
    {
        faker.RuleFor(x => x.PokerSessionId, sessionId);
        return faker;
    }

    public static PokerRoundFaker WithLabel(this PokerRoundFaker faker, string label)
    {
        faker.RuleFor(x => x.Label, label);
        return faker;
    }

    public static PokerRoundFaker WithStatus(this PokerRoundFaker faker, PokerRoundStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static PokerRoundFaker WithOrder(this PokerRoundFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }

    public static PokerRoundFaker WithConsensusEstimate(this PokerRoundFaker faker, string estimate)
    {
        faker.RuleFor(x => x.ConsensusEstimate, estimate);
        return faker;
    }
}
