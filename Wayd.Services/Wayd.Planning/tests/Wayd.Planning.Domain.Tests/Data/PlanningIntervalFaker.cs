using Wayd.Common.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;
using NodaTime.Extensions;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PlanningIntervalFaker : PrivateConstructorFaker<PlanningInterval>
{
    public PlanningIntervalFaker()
    {
        PlanningIntervalId = FakerHub.Random.Guid();
        RuleFor(x => x.Id, PlanningIntervalId);
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.Name, f => f.Random.String2(10));
        RuleFor(x => x.Description, f => f.Random.String2(10));
        RuleFor(x => x.DateRange, f => new LocalDateRange(f.Date.Past().ToLocalDateTime().Date, f.Date.Future().ToLocalDateTime().Date));
        RuleFor(x => x.ObjectivesLocked, false);
    }

    public Guid PlanningIntervalId { get; init; }
}

public static class PlanningIntervalFakerExtensions
{
    public static PlanningIntervalFaker WithId(this PlanningIntervalFaker faker, Guid planningIntervalId)
    {
        faker.RuleFor(x => x.Id, planningIntervalId);
        return faker;
    }

    public static PlanningIntervalFaker WithKey(this PlanningIntervalFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static PlanningIntervalFaker WithName(this PlanningIntervalFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PlanningIntervalFaker WithDescription(this PlanningIntervalFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static PlanningIntervalFaker WithDateRange(this PlanningIntervalFaker faker, LocalDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);
        return faker;
    }

    public static PlanningIntervalFaker WithObjectivesLocked(this PlanningIntervalFaker faker, bool objectivesLocked)
    {
        faker.RuleFor(x => x.ObjectivesLocked, objectivesLocked);
        return faker;
    }

    public static PlanningIntervalFaker WithIterations(this PlanningIntervalFaker faker, LocalDateRange planningIntervalDates, int iterationWeeks = 2, string? iterationPrefix = "Iteration ")
    {
        var planningInterval = PlanningInterval.Create("Test", null, planningIntervalDates, iterationWeeks, iterationPrefix);

        foreach (var iteration in planningInterval.Value.Iterations.ToList())
        {
            iteration.SetPrivate(m => m.Id, Guid.NewGuid());
            iteration.SetPrivate(m => m.PlanningIntervalId, faker.PlanningIntervalId);
        }

        faker.RuleFor("_iterations", f => planningInterval.Value.Iterations.ToList());
        faker.RuleFor(x => x.DateRange, planningIntervalDates);
        return faker;
    }

    public static PlanningIntervalFaker WithObjectives(this PlanningIntervalFaker faker, PlanningTeam team, int objectivesCount = 0)
    {
        faker.RuleFor("_objectives", f => new PlanningIntervalObjectiveFaker(faker.PlanningIntervalId, team, Enums.ObjectiveStatus.NotStarted, false).Generate(objectivesCount));
        return faker;
    }

    public static PlanningIntervalFaker WithTeams(this PlanningIntervalFaker faker, params Guid[] teamIds)
    {
        var teams = teamIds.Select(teamId => new PlanningIntervalTeamFaker(faker.PlanningIntervalId, teamId).Generate()).ToList();
        faker.RuleFor("_teams", f => teams);
        return faker;
    }

    public static PlanningIntervalFaker WithIterationSprints(this PlanningIntervalFaker faker, params PlanningIntervalIterationSprint[] sprints)
    {
        faker.RuleFor("_iterationSprints", f => sprints.ToList());
        return faker;
    }
}
