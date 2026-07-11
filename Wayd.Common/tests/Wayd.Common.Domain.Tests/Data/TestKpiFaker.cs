using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Domain.Tests.Data.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class TestKpiFaker : PrivateConstructorFaker<TestKpi>
{
    public TestKpiFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Lorem.Sentence());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.TargetValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.StartingValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.ActualValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.Prefix, f => f.PickRandom<string?>(null, "$", "€"));
        RuleFor(x => x.Suffix, f => f.PickRandom<string?>(null, "%", "K", "M"));
        RuleFor(x => x.TargetDirection, f => f.PickRandom<KpiTargetDirection>());
    }
}

public static class TestKpiFakerExtensions
{
    public static TestKpiFaker WithId(this TestKpiFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static TestKpiFaker WithName(this TestKpiFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static TestKpiFaker WithDescription(this TestKpiFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static TestKpiFaker WithStartingValue(this TestKpiFaker faker, double? startingValue)
    {
        faker.RuleFor(x => x.StartingValue, startingValue);

        return faker;
    }

    public static TestKpiFaker WithTargetValue(this TestKpiFaker faker, double? targetValue)
    {
        faker.RuleFor(x => x.TargetValue, targetValue);

        return faker;
    }

    public static TestKpiFaker WithActualValue(this TestKpiFaker faker, double? actualValue)
    {
        faker.RuleFor(x => x.ActualValue, actualValue);

        return faker;
    }

    public static TestKpiFaker WithPrefix(this TestKpiFaker faker, string? prefix)
    {
        faker.RuleFor(x => x.Prefix, prefix);

        return faker;
    }

    public static TestKpiFaker WithSuffix(this TestKpiFaker faker, string? suffix)
    {
        faker.RuleFor(x => x.Suffix, suffix);

        return faker;
    }

    public static TestKpiFaker WithKpiTargetDirection(this TestKpiFaker faker, KpiTargetDirection? kpiTargetDirection)
    {
        faker.RuleFor(x => x.TargetDirection, kpiTargetDirection);

        return faker;
    }
}
