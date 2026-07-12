using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class EstimationScaleFaker : PrivateConstructorFaker<EstimationScale>
{
    public EstimationScaleFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Lorem.Word());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.IsActive, true);
    }
}

public static class EstimationScaleFakerExtensions
{
    public static EstimationScaleFaker WithId(this EstimationScaleFaker faker, int id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static EstimationScaleFaker WithName(this EstimationScaleFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static EstimationScaleFaker WithDescription(this EstimationScaleFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static EstimationScaleFaker WithIsActive(this EstimationScaleFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
