using Wayd.Common.Models;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.StrategicManagement.Domain.Tests.Data;

public sealed class StrategyFaker : PrivateConstructorFaker<Strategy>
{
    public StrategyFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Company.CatchPhrase());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.Status, f => f.PickRandom<StrategyStatus>());
        RuleFor(x => x.Dates, f => null);
    }
}

public static class StrategyFakerExtensions
{
    public static StrategyFaker WithId(this StrategyFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static StrategyFaker WithKey(this StrategyFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static StrategyFaker WithName(this StrategyFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static StrategyFaker WithDescription(this StrategyFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static StrategyFaker WithStatus(this StrategyFaker faker, StrategyStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static StrategyFaker WithDates(this StrategyFaker faker, FlexibleDateRange? dates)
    {
        faker.RuleFor(x => x.Dates, dates);
        return faker;
    }

    public static StrategyFaker AsDraft(this StrategyFaker faker)
    {
        faker.RuleFor(x => x.Status, StrategyStatus.Draft);
        faker.RuleFor(x => x.Dates, f => null);
        return faker;
    }

    public static StrategyFaker AsActive(this StrategyFaker faker)
    {
        faker.RuleFor(x => x.Status, StrategyStatus.Active);
        return faker;
    }

    public static StrategyFaker AsCompleted(this StrategyFaker faker)
    {
        faker.RuleFor(x => x.Status, StrategyStatus.Completed);
        return faker;
    }

    public static StrategyFaker AsArchived(this StrategyFaker faker)
    {
        faker.RuleFor(x => x.Status, StrategyStatus.Archived);
        return faker;
    }
}
