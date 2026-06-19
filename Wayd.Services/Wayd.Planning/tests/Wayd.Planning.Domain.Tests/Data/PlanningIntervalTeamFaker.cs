using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public sealed class PlanningIntervalTeamFaker : PrivateConstructorFaker<PlanningIntervalTeam>
{
    public PlanningIntervalTeamFaker()
    {
        RuleFor(x => x.PlanningIntervalId, f => f.Random.Guid());
        RuleFor(x => x.TeamId, f => f.Random.Guid());
    }

    public PlanningIntervalTeamFaker(Guid planningIntervalId, Guid teamId)
    {
        RuleFor(x => x.PlanningIntervalId, planningIntervalId);
        RuleFor(x => x.TeamId, teamId);
    }
}

public static class PlanningIntervalTeamFakerExtensions
{
    public static PlanningIntervalTeamFaker WithPlanningIntervalId(this PlanningIntervalTeamFaker faker, Guid planningIntervalId)
    {
        faker.RuleFor(x => x.PlanningIntervalId, planningIntervalId);
        return faker;
    }

    public static PlanningIntervalTeamFaker WithTeamId(this PlanningIntervalTeamFaker faker, Guid teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);
        return faker;
    }
}
