using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class StrategicThemeFaker : PrivateConstructorFaker<StrategicTheme>
{
    public StrategicThemeFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.Name, f => f.Lorem.Word());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.State, f => StrategicThemeState.Active);
    }
}

public static class StrategicThemeFakerExtensions
{
    public static StrategicThemeFaker WithId(this StrategicThemeFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static StrategicThemeFaker WithKey(this StrategicThemeFaker faker, int? key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static StrategicThemeFaker WithName(this StrategicThemeFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static StrategicThemeFaker WithDescription(this StrategicThemeFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static StrategicThemeFaker WithState(this StrategicThemeFaker faker, StrategicThemeState? state)
    {
        faker.RuleFor(x => x.State, state);

        return faker;
    }
}