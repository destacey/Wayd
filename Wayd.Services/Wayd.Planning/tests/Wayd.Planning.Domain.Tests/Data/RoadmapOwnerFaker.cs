using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapOwnerFaker : PrivateConstructorFaker<RoadmapOwner>
{
    public RoadmapOwnerFaker(Guid roadmapId)
    {
        RuleFor(x => x.RoadmapId, roadmapId);
        RuleFor(x => x.EmployeeId, f => f.Random.Guid());
    }
}

public static class RoadmapOwnerFakerExtensions
{
    public static RoadmapOwnerFaker WithRoadmapId(this RoadmapOwnerFaker faker, Guid? roadmapId)
    {
        faker.RuleFor(x => x.RoadmapId, roadmapId);

        return faker;
    }

    public static RoadmapOwnerFaker WithEmployeeId(this RoadmapOwnerFaker faker, Guid? employeeId)
    {
        faker.RuleFor(x => x.EmployeeId, employeeId);

        return faker;
    }
}
