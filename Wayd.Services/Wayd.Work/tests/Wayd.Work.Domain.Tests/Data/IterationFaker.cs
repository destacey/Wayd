using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;
using NodaTime;

namespace Wayd.Work.Domain.Tests.Data;

public sealed class IterationFaker : PrivateConstructorFaker<Iteration>
{
    public IterationFaker()
    {
        var today = SystemClock.Instance.GetCurrentInstant().InUtc().Date;
        var start = today.PlusDays(-7);
        var end = today.PlusDays(7);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Company.CatchPhrase());
        RuleFor(x => x.Type, f => f.PickRandom<IterationType>());
        RuleFor(x => x.DateRange, f => IterationDateRange.Create(start, end));
        RuleFor(x => x.TeamId, f => f.Random.Guid());
        RuleFor(x => x.OwnershipInfo, f => OwnershipInfo.CreateWaydOwned());
    }
}

public static class IterationFakerExtensions
{
    public static IterationFaker WithId(this IterationFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static IterationFaker WithKey(this IterationFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static IterationFaker WithName(this IterationFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static IterationFaker WithType(this IterationFaker faker, IterationType type)
    {
        faker.RuleFor(x => x.Type, type);
        return faker;
    }

    public static IterationFaker WithDateRange(this IterationFaker faker, IterationDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);
        return faker;
    }

    public static IterationFaker WithTeamId(this IterationFaker faker, Guid? teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);
        return faker;
    }

    public static IterationFaker WithTeam(this IterationFaker faker, WorkTeam team)
    {
        faker.RuleFor(x => x.TeamId, team.Id);
        faker.RuleFor(x => x.Team, team);
        return faker;
    }

    public static IterationFaker WithStarted(this IterationFaker faker, Instant? started)
    {
        faker.RuleFor(x => x.Started, started);
        return faker;
    }

    public static IterationFaker WithCompleted(this IterationFaker faker, Instant? completed)
    {
        faker.RuleFor(x => x.Completed, completed);
        return faker;
    }

    public static IterationFaker WithSprintTypeOverride(this IterationFaker faker, SprintType? sprintTypeOverride)
    {
        faker.RuleFor(x => x.SprintTypeOverride, sprintTypeOverride);
        return faker;
    }

    public static IterationFaker WithOwnershipInfo(this IterationFaker faker, OwnershipInfo ownershipInfo)
    {
        faker.RuleFor(x => x.OwnershipInfo, ownershipInfo);
        return faker;
    }

    /// <summary>
    /// Creates a two-week iteration whose last day is <paramref name="endDate"/>.
    /// </summary>
    public static IterationFaker WithEndDate(this IterationFaker faker, LocalDate endDate, IterationType type = IterationType.Sprint)
    {
        faker.RuleFor(x => x.Type, type);
        faker.RuleFor(x => x.DateRange, new IterationDateRange(endDate.PlusDays(-13), endDate));
        return faker;
    }

    public static IterationFaker AsIteration(this IterationFaker faker)
    {
        faker.RuleFor(x => x.Type, IterationType.Iteration);
        return faker;
    }

    public static IterationFaker AsSprint(this IterationFaker faker)
    {
        faker.RuleFor(x => x.Type, IterationType.Sprint);
        return faker;
    }

    public static IterationFaker AsManaged(this IterationFaker faker, Connector connector = Connector.AzureDevOps, string? systemId = null, string? externalId = null)
    {
        faker.CustomInstantiator(f =>
        {
            var actualSystemId = systemId ?? f.Random.AlphaNumeric(10);
            var actualExternalId = externalId ?? f.Random.AlphaNumeric(10);
            var iteration = faker.Generate();
            var ownershipInfo = OwnershipInfo.CreateExternalOwned(connector, actualSystemId, actualExternalId);

            // Create a new faker with the ownership info
            return new IterationFaker()
                .WithOwnershipInfo(ownershipInfo)
                .Generate();
        });
        return faker;
    }
}
