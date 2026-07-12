using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;
using NodaTime.Extensions;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PokerVoteFaker : PrivateConstructorFaker<PokerVote>
{
    private static readonly string[] ScaleValues = ["1", "2", "3", "5", "8", "13", "21", "?"];

    public PokerVoteFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.PokerRoundId, f => f.Random.Guid());
        RuleFor(x => x.ParticipantId, f => f.Random.Guid().ToString());
        RuleFor(x => x.Value, f => f.PickRandom(ScaleValues));
        RuleFor(x => x.SubmittedOn, f => f.Date.Recent().ToUniversalTime().ToInstant());
    }
}

public static class PokerVoteFakerExtensions
{
    public static PokerVoteFaker WithId(this PokerVoteFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PokerVoteFaker WithPokerRoundId(this PokerVoteFaker faker, Guid roundId)
    {
        faker.RuleFor(x => x.PokerRoundId, roundId);
        return faker;
    }

    public static PokerVoteFaker WithParticipantId(this PokerVoteFaker faker, string participantId)
    {
        faker.RuleFor(x => x.ParticipantId, participantId);
        return faker;
    }

    public static PokerVoteFaker WithValue(this PokerVoteFaker faker, string value)
    {
        faker.RuleFor(x => x.Value, value);
        return faker;
    }

    public static PokerVoteFaker WithSubmittedOn(this PokerVoteFaker faker, Instant submittedOn)
    {
        faker.RuleFor(x => x.SubmittedOn, submittedOn);
        return faker;
    }
}
