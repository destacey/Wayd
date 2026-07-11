using Wayd.Common.Domain.Enums;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemReferenceFaker : PrivateConstructorFaker<WorkItemReference>
{
    public WorkItemReferenceFaker()
    {
        RuleFor(x => x.WorkItemId, f => f.Random.Guid());
        RuleFor(x => x.ObjectId, f => f.Random.Guid());
        RuleFor(x => x.Context, f => f.PickRandom<SystemContext>());
    }
}

public static class WorkItemReferenceFakerExtensions
{
    public static WorkItemReferenceFaker WithWorkItemId(this WorkItemReferenceFaker faker, Guid workItemId)
    {
        faker.RuleFor(x => x.WorkItemId, workItemId);
        return faker;
    }

    public static WorkItemReferenceFaker WithObjectId(this WorkItemReferenceFaker faker, Guid objectId)
    {
        faker.RuleFor(x => x.ObjectId, objectId);
        return faker;
    }

    public static WorkItemReferenceFaker WithContext(this WorkItemReferenceFaker faker, SystemContext context)
    {
        faker.RuleFor(x => x.Context, context);
        return faker;
    }
}
