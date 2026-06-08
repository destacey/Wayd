using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapManagerFaker : PrivateConstructorFaker<RoadmapManager>
{
    public RoadmapManagerFaker(Guid roadmapId)
    {
        RuleFor(x => x.RoadmapId, roadmapId);
        RuleFor(x => x.ManagerId, f => f.Random.Guid());
    }
}

public static class RoadmapManagerFakerExtensions
{
    public static RoadmapManagerFaker WithRoadmapId(this RoadmapManagerFaker faker, Guid? roadmapId)
    {
        faker.RuleFor(x => x.RoadmapId, roadmapId);

        return faker;
    }

    public static RoadmapManagerFaker WithManagerId(this RoadmapManagerFaker faker, Guid? managerId)
    {
        faker.RuleFor(x => x.ManagerId, managerId);

        return faker;
    }
}