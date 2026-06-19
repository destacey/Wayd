using Bogus;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Domain.Tests.Data;

/// <summary>
/// Faker for VisionAggregate. Since VisionAggregate is an aggregate of Vision entities,
/// this faker creates a VisionAggregate with a collection of visions.
/// </summary>
public sealed class VisionAggregateFaker : Faker<VisionAggregate>
{
    public VisionAggregateFaker()
    {
        CustomInstantiator(f =>
        {
            var visions = new VisionFaker().Generate(f.Random.Int(1, 3));
            return new VisionAggregate(visions);
        });
    }
}

public static class VisionAggregateFakerExtensions
{
    public static VisionAggregateFaker WithVisions(this VisionAggregateFaker faker, List<Vision> visions)
    {
        faker.CustomInstantiator(f => new VisionAggregate(visions));
        return faker;
    }

    public static VisionAggregateFaker WithSingleVision(this VisionAggregateFaker faker, Vision vision)
    {
        faker.CustomInstantiator(f => new VisionAggregate([vision]));
        return faker;
    }

    public static VisionAggregateFaker WithProposedVision(this VisionAggregateFaker faker)
    {
        faker.CustomInstantiator(f =>
        {
            var vision = new VisionFaker().Generate();
            return new VisionAggregate([vision]);
        });
        return faker;
    }

    public static VisionAggregateFaker WithActiveVision(this VisionAggregateFaker faker)
    {
        faker.CustomInstantiator(f =>
        {
            var vision = new VisionFaker().ActiveVision();
            return new VisionAggregate([vision]);
        });
        return faker;
    }

    public static VisionAggregateFaker WithArchivedVision(this VisionAggregateFaker faker)
    {
        faker.CustomInstantiator(f =>
        {
            var vision = new VisionFaker().ArchivedVision();
            return new VisionAggregate([vision]);
        });
        return faker;
    }
}
