using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Planning.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public sealed class PlanningSprintFaker : PrivateConstructorFaker<PlanningSprint>
{
    public PlanningSprintFaker()
    {
        var today = SystemClock.Instance.GetCurrentInstant().InUtc().Date;
        var start = today.PlusDays(-7);
        var end = today.PlusDays(7);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Company.CatchPhrase());
        RuleFor(x => x.Type, f => IterationType.Sprint);
        RuleFor(x => x.DateRange, f => IterationDateRange.Create(start, end));
        RuleFor(x => x.TeamId, f => f.Random.Guid());
    }
}

public static class PlanningSprintFakerExtensions
{
    public static PlanningSprintFaker WithId(this PlanningSprintFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PlanningSprintFaker WithKey(this PlanningSprintFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static PlanningSprintFaker WithName(this PlanningSprintFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PlanningSprintFaker WithType(this PlanningSprintFaker faker, IterationType type)
    {
        faker.RuleFor(x => x.Type, type);
        return faker;
    }

    public static PlanningSprintFaker WithDateRange(this PlanningSprintFaker faker, IterationDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);
        return faker;
    }

    public static PlanningSprintFaker WithTeamId(this PlanningSprintFaker faker, Guid? teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);
        return faker;
    }

    public static PlanningSprintFaker WithWatermarks(this PlanningSprintFaker faker, PlanningSprintWatermarks watermarks)
    {
        faker.RuleFor(x => x.Watermarks, watermarks);
        return faker;
    }

    public static PlanningSprintFaker AsIteration(this PlanningSprintFaker faker)
    {
        faker.RuleFor(x => x.Type, IterationType.Iteration);
        return faker;
    }

    public static PlanningSprintFaker AsSprint(this PlanningSprintFaker faker)
    {
        faker.RuleFor(x => x.Type, IterationType.Sprint);
        return faker;
    }
}
