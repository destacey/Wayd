using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class PpmTeamFaker : PrivateConstructorFaker<PpmTeam>
{
    public PpmTeamFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 100000));
        RuleFor(x => x.Name, f => f.Random.String2(15));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5).ToUpperInvariant()));
        RuleFor(x => x.Type, TeamType.Team);
        RuleFor(x => x.IsActive, true);
    }
}

public static class PpmTeamFakerExtensions
{
    public static PpmTeamFaker WithId(this PpmTeamFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PpmTeamFaker WithName(this PpmTeamFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PpmTeamFaker WithCode(this PpmTeamFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static PpmTeamFaker WithIsActive(this PpmTeamFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
