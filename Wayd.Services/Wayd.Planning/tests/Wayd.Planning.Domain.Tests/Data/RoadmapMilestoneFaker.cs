using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapMilestoneFaker : PrivateConstructorFaker<RoadmapMilestone>
{
    public RoadmapMilestoneFaker(Guid? roadmapId = null, LocalDate? localDate = null)
    {
        BaseDate = localDate ?? LocalDate.FromDateTime(DateTime.Today);

        RuleFor(x => x.RoadmapId, f => roadmapId ?? f.Random.Guid());
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.Date, f => BaseDate);
        RuleFor(x => x.Color, f => string.Format("#{0:X6}", f.Random.Hexadecimal(0x1000000)));
        RuleFor(x => x.ParentId, f => null);
    }

    public LocalDate BaseDate { get; }
}

public static class RoadmapMilestoneFakerExtensions
{
    public static RoadmapMilestoneFaker WithId(this RoadmapMilestoneFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static RoadmapMilestoneFaker WithRoadmapId(this RoadmapMilestoneFaker faker, Guid? roadmapId)
    {
        faker.RuleFor(x => x.RoadmapId, roadmapId);

        return faker;
    }

    public static RoadmapMilestoneFaker WithName(this RoadmapMilestoneFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static RoadmapMilestoneFaker WithDescription(this RoadmapMilestoneFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static RoadmapMilestoneFaker WithDate(this RoadmapMilestoneFaker faker, LocalDate? date)
    {
        faker.RuleFor(x => x.Date, date);

        return faker;
    }

    public static RoadmapMilestoneFaker WithParentId(this RoadmapMilestoneFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static RoadmapMilestoneFaker WithParent(this RoadmapMilestoneFaker faker, RoadmapActivity parent)
    {
        faker.RuleFor(x => x.ParentId, parent.Id);
        faker.RuleFor(x => x.Parent, parent);

        return faker;
    }

    public static RoadmapMilestoneFaker WithColor(this RoadmapMilestoneFaker faker, string? color)
    {
        faker.RuleFor(x => x.Color, color);

        return faker;
    }
}