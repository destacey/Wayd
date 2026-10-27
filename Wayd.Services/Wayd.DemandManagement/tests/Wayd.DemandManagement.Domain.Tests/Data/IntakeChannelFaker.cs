using Wayd.DemandManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.DemandManagement.Domain.Tests.Data;

public sealed class IntakeChannelFaker : PrivateConstructorFaker<IntakeChannel>
{
    public IntakeChannelFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.PickRandom("Business stakeholder", "Customer feedback", "Support", "Security"));
        RuleFor(x => x.Order, f => f.Random.Int(1, 8));
        RuleFor(x => x.IsActive, true);
    }
}

public static class IntakeChannelFakerExtensions
{
    public static IntakeChannelFaker WithId(this IntakeChannelFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static IntakeChannelFaker WithName(this IntakeChannelFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static IntakeChannelFaker WithDescription(this IntakeChannelFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static IntakeChannelFaker WithOrder(this IntakeChannelFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static IntakeChannelFaker AsArchived(this IntakeChannelFaker faker)
    {
        faker.RuleFor(x => x.IsActive, false);

        return faker;
    }
}
