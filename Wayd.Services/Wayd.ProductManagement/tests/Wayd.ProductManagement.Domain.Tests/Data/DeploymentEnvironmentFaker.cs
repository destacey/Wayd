using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class DeploymentEnvironmentFaker : PrivateConstructorFaker<DeploymentEnvironment>
{
    public DeploymentEnvironmentFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.PickRandom("Development", "Test", "Staging", "Production"));
        RuleFor(x => x.Category, f => f.PickRandom<EnvironmentCategory>());
        RuleFor(x => x.RingOrder, f => f.Random.Int(1, 5));
        RuleFor(x => x.IsActive, f => true);
    }
}

public static class DeploymentEnvironmentFakerExtensions
{
    public static DeploymentEnvironmentFaker WithId(this DeploymentEnvironmentFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static DeploymentEnvironmentFaker WithKey(this DeploymentEnvironmentFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static DeploymentEnvironmentFaker WithName(this DeploymentEnvironmentFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static DeploymentEnvironmentFaker WithCategory(this DeploymentEnvironmentFaker faker, EnvironmentCategory category)
    {
        faker.RuleFor(x => x.Category, category);

        return faker;
    }

    public static DeploymentEnvironmentFaker WithRingOrder(this DeploymentEnvironmentFaker faker, int ringOrder)
    {
        faker.RuleFor(x => x.RingOrder, ringOrder);

        return faker;
    }

    public static DeploymentEnvironmentFaker WithIsActive(this DeploymentEnvironmentFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);

        return faker;
    }

    public static DeploymentEnvironmentFaker AsProduction(this DeploymentEnvironmentFaker faker)
    {
        faker.RuleFor(x => x.Name, "Production");
        faker.RuleFor(x => x.Category, EnvironmentCategory.Production);

        return faker;
    }

    public static DeploymentEnvironmentFaker AsRetired(this DeploymentEnvironmentFaker faker)
    {
        faker.RuleFor(x => x.IsActive, false);

        return faker;
    }
}
