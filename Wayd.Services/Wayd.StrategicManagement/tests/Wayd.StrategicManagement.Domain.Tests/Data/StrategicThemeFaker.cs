using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.StrategicManagement.Domain.Tests.Data;

public sealed class StrategicThemeFaker : PrivateConstructorFaker<StrategicTheme>
{
    public StrategicThemeFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Commerce.Department());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.State, f => f.PickRandom<StrategicThemeState>());
    }
}

public static class StrategicThemeFakerExtensions
{
    public static StrategicThemeFaker WithId(this StrategicThemeFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static StrategicThemeFaker WithKey(this StrategicThemeFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static StrategicThemeFaker WithName(this StrategicThemeFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static StrategicThemeFaker WithDescription(this StrategicThemeFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static StrategicThemeFaker WithState(this StrategicThemeFaker faker, StrategicThemeState state)
    {
        faker.RuleFor(x => x.State, state);
        return faker;
    }

    public static StrategicThemeFaker AsProposed(this StrategicThemeFaker faker)
    {
        faker.RuleFor(x => x.State, StrategicThemeState.Proposed);
        return faker;
    }

    public static StrategicThemeFaker AsActive(this StrategicThemeFaker faker)
    {
        faker.RuleFor(x => x.State, StrategicThemeState.Active);
        return faker;
    }

    public static StrategicThemeFaker AsArchived(this StrategicThemeFaker faker)
    {
        faker.RuleFor(x => x.State, StrategicThemeState.Archived);
        return faker;
    }
}
