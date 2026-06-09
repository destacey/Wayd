using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class StrategicInitiativeKpiFaker : PrivateConstructorFaker<StrategicInitiativeKpi>
{
    public StrategicInitiativeKpiFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Lorem.Sentence());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.TargetValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.ActualValue, f => null);
        RuleFor(x => x.Prefix, f => f.PickRandom<string?>(null, "$", "€"));
        RuleFor(x => x.Suffix, f => f.PickRandom<string?>(null, "%", "K", "M"));
        RuleFor(x => x.TargetDirection, f => f.PickRandom<KpiTargetDirection>());
        RuleFor(x => x.StrategicInitiativeId, f => f.Random.Guid());
        RuleFor(x => x.Order, f => f.Random.Int(1, 100));
    }
}

public static class StrategicInitiativeKpiFakerExtensions
{
    public static StrategicInitiativeKpiFaker WithId(this StrategicInitiativeKpiFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithName(this StrategicInitiativeKpiFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithDescription(this StrategicInitiativeKpiFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithTargetValue(this StrategicInitiativeKpiFaker faker, double targetValue)
    {
        faker.RuleFor(x => x.TargetValue, targetValue);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithActualValue(this StrategicInitiativeKpiFaker faker, double? actualValue)
    {
        faker.RuleFor(x => x.ActualValue, actualValue);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithPrefix(this StrategicInitiativeKpiFaker faker, string? prefix)
    {
        faker.RuleFor(x => x.Prefix, prefix);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithSuffix(this StrategicInitiativeKpiFaker faker, string? suffix)
    {
        faker.RuleFor(x => x.Suffix, suffix);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithKpiTargetDirection(this StrategicInitiativeKpiFaker faker, KpiTargetDirection kpiTargetDirection)
    {
        faker.RuleFor(x => x.TargetDirection, kpiTargetDirection);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithStrategicInitiativeId(this StrategicInitiativeKpiFaker faker, Guid strategicInitiativeId)
    {
        faker.RuleFor(x => x.StrategicInitiativeId, strategicInitiativeId);

        return faker;
    }

    public static StrategicInitiativeKpiFaker WithOrder(this StrategicInitiativeKpiFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }
}