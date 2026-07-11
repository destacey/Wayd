using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkTeamFaker : PrivateConstructorFaker<WorkTeam>
{
    public WorkTeamFaker(TeamType type)
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.Name, f => f.Random.String2(15));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Type, type);
        RuleFor(x => x.IsActive, true);
    }
}

public static class WorkTeamFakerExtensions
{
    public static WorkTeamFaker WithId(this WorkTeamFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkTeamFaker WithKey(this WorkTeamFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static WorkTeamFaker WithName(this WorkTeamFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkTeamFaker WithCode(this WorkTeamFaker faker, string code)
    {
        faker.RuleFor(x => x.Code, new TeamCode(code));
        return faker;
    }

    public static WorkTeamFaker WithCode(this WorkTeamFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static WorkTeamFaker WithIsActive(this WorkTeamFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
