using NodaTime.Extensions;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PlanningIntervalIterationFaker : PrivateConstructorFaker<PlanningIntervalIteration>
{
    public PlanningIntervalIterationFaker(Guid? planningIntervalId = null)
    {
        var piId = planningIntervalId ?? FakerHub.Random.Guid();
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.PlanningIntervalId, piId);
        RuleFor(x => x.Name, f => f.Random.String2(10));
        RuleFor(x => x.Category, f => f.PickRandom<IterationCategory>());
        RuleFor(x => x.DateRange, f => new LocalDateRange(f.Date.Past().ToLocalDateTime().Date, f.Date.Future().ToLocalDateTime().Date));
    }
}

public static class PlanningIntervalIterationFakerExtensions
{
    public static PlanningIntervalIterationFaker WithPlanningIntervalId(this PlanningIntervalIterationFaker faker, Guid? planningIntervalId)
    {
        faker.RuleFor(x => x.PlanningIntervalId, planningIntervalId);

        return faker;
    }

    public static PlanningIntervalIterationFaker WithName(this PlanningIntervalIterationFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static PlanningIntervalIterationFaker WithIterationCategory(this PlanningIntervalIterationFaker faker, IterationCategory? iterationCategory)
    {
        faker.RuleFor(x => x.Category, iterationCategory);

        return faker;
    }

    public static PlanningIntervalIterationFaker WithDateRange(this PlanningIntervalIterationFaker faker, LocalDateRange? dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static PlanningIntervalIterationFaker WithSprints(this PlanningIntervalIterationFaker faker, params PlanningIntervalIterationSprint[] sprints)
    {
        faker.RuleFor("_iterationSprints", f => sprints.ToList());
        return faker;
    }
}
