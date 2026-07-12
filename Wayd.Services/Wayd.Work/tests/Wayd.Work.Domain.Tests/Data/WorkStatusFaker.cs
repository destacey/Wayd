using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkStatusFaker : PrivateConstructorFaker<WorkStatus>
{
    public WorkStatusFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Number(1, 10000));
        RuleFor(x => x.Name, f => f.Random.String2(10));
        RuleFor(x => x.Description, f => f.Random.String2(10));
        RuleFor(x => x.IsActive, true);
    }
}

public static class WorkStatusFakerExtensions
{
    public static WorkStatusFaker WithId(this WorkStatusFaker faker, int id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkStatusFaker WithName(this WorkStatusFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkStatusFaker WithDescription(this WorkStatusFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static WorkStatusFaker WithIsActive(this WorkStatusFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
