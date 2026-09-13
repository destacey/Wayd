using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PlanningTeamFaker : PrivateConstructorFaker<PlanningTeam>
{
    public PlanningTeamFaker(TeamType type)
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.Name, f => f.Random.String2(15));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Type, type);
        RuleFor(x => x.IsActive, true);
    }
}

public static class PlanningTeamFakerExtensions
{
    public static PlanningTeamFaker WithId(this PlanningTeamFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PlanningTeamFaker WithName(this PlanningTeamFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PlanningTeamFaker WithCode(this PlanningTeamFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static PlanningTeamFaker WithIsActive(this PlanningTeamFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
