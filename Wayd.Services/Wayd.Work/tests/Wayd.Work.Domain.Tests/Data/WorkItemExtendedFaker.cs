using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkItemExtendedFaker : PrivateConstructorFaker<WorkItemExtended>
{
    public WorkItemExtendedFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ExternalTeamIdentifier, f => f.Random.AlphaNumeric(10));
    }
}

public static class WorkItemExtendedFakerExtensions
{
    public static WorkItemExtendedFaker WithId(this WorkItemExtendedFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkItemExtendedFaker WithExternalTeamIdentifier(this WorkItemExtendedFaker faker, string externalTeamIdentifier)
    {
        faker.RuleFor(x => x.ExternalTeamIdentifier, externalTeamIdentifier);
        return faker;
    }
}
