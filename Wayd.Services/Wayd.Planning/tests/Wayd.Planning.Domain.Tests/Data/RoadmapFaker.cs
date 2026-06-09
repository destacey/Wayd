using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapFaker : PrivateConstructorFaker<Roadmap>
{
    public RoadmapFaker(LocalDate? localDate = null)
    {
        BaseDate = localDate ?? LocalDate.FromDateTime(DateTime.Today);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.DateRange, f => new LocalDateRange(BaseDate, BaseDate.PlusDays(10)));
        RuleFor(x => x.Visibility, f => f.PickRandom<Visibility>());
        RuleFor(x => x.State, f => RoadmapState.Active);
        //RuleFor(x => x.Managers, f => managerFaker.Generate(1)); // TODO not working
    }

    public LocalDate BaseDate { get; }
}

public static class RoadmapFakerExtensions
{
    public static RoadmapFaker WithId(this RoadmapFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static RoadmapFaker WithName(this RoadmapFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static RoadmapFaker WithDescription(this RoadmapFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static RoadmapFaker WithDateRange(this RoadmapFaker faker, LocalDateRange? dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static RoadmapFaker WithVisibility(this RoadmapFaker faker, Visibility? visibility)
    {
        faker.RuleFor(x => x.Visibility, visibility);

        return faker;
    }

    public static RoadmapFaker WithState(this RoadmapFaker faker, RoadmapState? state)
    {
        faker.RuleFor(x => x.State, state);

        return faker;
    }

    //public static Roadmap WithChildren(this RoadmapFaker faker, int childrenCount)
    //{
    //    var roadmapId = Guid.NewGuid();

    //    var childFaker = new RoadmapFaker(faker.BaseDate);

    //    List<Roadmap> children = new(childrenCount);
    //    for (int i = 0; i < childrenCount; i++)
    //    {
    //        var child = childFaker.WithParentId(roadmapId).WithOrder(i + 1).Generate();
    //        children.Add(child);
    //    }

    //    var managers = new RoadmapManagerFaker(roadmapId).Generate(1);

    //    faker.RuleFor("_children", f => children.ToList());
    //    faker.RuleFor("_roadmapManagers", f => managers.ToList());

    //    return faker.WithId(roadmapId).Generate();
    //}
}