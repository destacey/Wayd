using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class ProductTeamFaker : PrivateConstructorFaker<ProductTeam>
{
    public ProductTeamFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 100000));
        RuleFor(x => x.Name, f => f.Random.String2(15));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5).ToUpperInvariant()));
        RuleFor(x => x.Type, TeamType.Team);
        RuleFor(x => x.IsActive, true);
    }
}

public static class ProductTeamFakerExtensions
{
    public static ProductTeamFaker WithId(this ProductTeamFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static ProductTeamFaker WithName(this ProductTeamFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static ProductTeamFaker WithCode(this ProductTeamFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static ProductTeamFaker WithIsActive(this ProductTeamFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }
}
