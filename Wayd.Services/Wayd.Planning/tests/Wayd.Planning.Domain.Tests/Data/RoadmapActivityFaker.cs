using System.Reflection;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapActivityFaker : PrivateConstructorFaker<RoadmapActivity>
{
    public RoadmapActivityFaker(Guid? roadmapId = null, LocalDate? localDate = null)
    {
        BaseDate = localDate ?? LocalDate.FromDateTime(DateTime.Today);

        RuleFor(x => x.RoadmapId, f => roadmapId ?? f.Random.Guid());
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.DateRange, f => new LocalDateRange(BaseDate, BaseDate.PlusDays(10)));
        RuleFor(x => x.Color, f => string.Format("#{0:X6}", f.Random.Hexadecimal(0x1000000)));
        RuleFor(x => x.ParentId, f => null);
        RuleFor(x => x.Order, f => f.Random.Int(1, 1000));
    }

    public LocalDate BaseDate { get; }
}

public static class RoadmapActivityFakerExtensions
{
    public static RoadmapActivityFaker WithId(this RoadmapActivityFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static RoadmapActivityFaker WithRoadmapId(this RoadmapActivityFaker faker, Guid? roadmapId)
    {
        faker.RuleFor(x => x.RoadmapId, roadmapId);

        return faker;
    }

    public static RoadmapActivityFaker WithName(this RoadmapActivityFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static RoadmapActivityFaker WithDescription(this RoadmapActivityFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static RoadmapActivityFaker WithDateRange(this RoadmapActivityFaker faker, LocalDateRange? dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static RoadmapActivityFaker WithParentId(this RoadmapActivityFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static RoadmapActivityFaker WithParent(this RoadmapActivityFaker faker, RoadmapActivity parent)
    {
        faker.RuleFor(x => x.ParentId, parent.Id);
        faker.RuleFor(x => x.Parent, parent);

        return faker;
    }

    public static RoadmapActivityFaker WithOrder(this RoadmapActivityFaker faker, int? order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static RoadmapActivityFaker WithColor(this RoadmapActivityFaker faker, string? color)
    {
        faker.RuleFor(x => x.Color, color);

        return faker;
    }

    public static RoadmapActivity WithChildren(this RoadmapActivityFaker faker, int childrenCount)
    {
        var activity = faker.Generate();

        var childFaker = new RoadmapActivityFaker(localDate: faker.BaseDate);

        List<BaseRoadmapItem> children = new(childrenCount);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = childFaker.WithParent(activity).WithOrder(i + 1).Generate();
            children.Add(child);
        }

        typeof(RoadmapActivity).GetField("_children", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(activity, children);

        return activity;
    }
}