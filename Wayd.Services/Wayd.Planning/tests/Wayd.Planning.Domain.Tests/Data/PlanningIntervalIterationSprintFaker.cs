using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public class PlanningIntervalIterationSprintFaker : PrivateConstructorFaker<PlanningIntervalIterationSprint>
{
    public PlanningIntervalIterationSprintFaker(Guid planningIntervalId, Guid planningIntervalIterationId, Guid sprintId)
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.PlanningIntervalId, planningIntervalId);
        RuleFor(x => x.PlanningIntervalIterationId, planningIntervalIterationId);
        RuleFor(x => x.SprintId, sprintId);
    }
}

public static class PlanningIntervalIterationSprintFakerExtensions
{
    public static PlanningIntervalIterationSprintFaker WithId(this PlanningIntervalIterationSprintFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }
}
