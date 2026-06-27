using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class RoadmapColorFaker : PrivateConstructorFaker<RoadmapColor>
{
    public RoadmapColorFaker()
    {
        RuleFor(x => x.Color, f => string.Format("#{0:X6}", f.Random.Hexadecimal(0x1000000)));
        RuleFor(x => x.Name, f => f.Random.Words(2));
        RuleFor(x => x.Order, f => f.Random.Int(1, 1000));
        RuleFor(x => x.IsDefault, f => false);
    }
}

public static class RoadmapColorFakerExtensions
{
    public static RoadmapColorFaker WithColor(this RoadmapColorFaker faker, string? color)
    {
        faker.RuleFor(x => x.Color, color);

        return faker;
    }

    public static RoadmapColorFaker WithName(this RoadmapColorFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static RoadmapColorFaker WithOrder(this RoadmapColorFaker faker, int? order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static RoadmapColorFaker WithIsDefault(this RoadmapColorFaker faker, bool? isDefault)
    {
        faker.RuleFor(x => x.IsDefault, isDefault);

        return faker;
    }
}
