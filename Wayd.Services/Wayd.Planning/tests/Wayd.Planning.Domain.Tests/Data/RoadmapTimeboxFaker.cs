using Wayd.Common.Models;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapTimeboxFaker : PrivateConstructorFaker<RoadmapTimebox>
{
    public RoadmapTimeboxFaker(Guid? roadmapId = null, LocalDate? localDate = null)
    {
        BaseDate = localDate ?? LocalDate.FromDateTime(DateTime.Today);

        RuleFor(x => x.RoadmapId, f => roadmapId ?? f.Random.Guid());
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.DateRange, f => new LocalDateRange(BaseDate, BaseDate.PlusDays(10)));
        RuleFor(x => x.Color, f => string.Format("#{0:X6}", f.Random.Hexadecimal(0x1000000)));
        RuleFor(x => x.ParentId, f => null);
    }

    public LocalDate BaseDate { get; }
}

public static class RoadmapTimeboxFakerExtensions
{
    public static RoadmapTimeboxFaker WithId(this RoadmapTimeboxFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static RoadmapTimeboxFaker WithRoadmapId(this RoadmapTimeboxFaker faker, Guid? roadmapId)
    {
        faker.RuleFor(x => x.RoadmapId, roadmapId);

        return faker;
    }

    public static RoadmapTimeboxFaker WithName(this RoadmapTimeboxFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static RoadmapTimeboxFaker WithDescription(this RoadmapTimeboxFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static RoadmapTimeboxFaker WithDateRange(this RoadmapTimeboxFaker faker, LocalDateRange? dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static RoadmapTimeboxFaker WithParentId(this RoadmapTimeboxFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static RoadmapTimeboxFaker WithParent(this RoadmapTimeboxFaker faker, RoadmapActivity parent)
    {
        faker.RuleFor(x => x.ParentId, parent.Id);
        faker.RuleFor(x => x.Parent, parent);

        return faker;
    }

    public static RoadmapTimeboxFaker WithColor(this RoadmapTimeboxFaker faker, string? color)
    {
        faker.RuleFor(x => x.Color, color);

        return faker;
    }
}