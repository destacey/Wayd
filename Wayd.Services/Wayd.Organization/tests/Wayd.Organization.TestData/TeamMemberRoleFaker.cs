using Wayd.Organization.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class TeamMemberRoleFaker : PrivateConstructorFaker<TeamMemberRole>
{
    public TeamMemberRoleFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.IsActive, f => true);
    }
}

public static class TeamMemberRoleFakerExtensions
{
    public static TeamMemberRoleFaker WithId(this TeamMemberRoleFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamMemberRoleFaker WithName(this TeamMemberRoleFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static TeamMemberRoleFaker WithDescription(this TeamMemberRoleFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static TeamMemberRoleFaker WithIsActive(this TeamMemberRoleFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static TeamMemberRoleFaker AsActive(this TeamMemberRoleFaker faker)
    {
        faker.RuleFor(x => x.IsActive, true);
        return faker;
    }

    public static TeamMemberRoleFaker AsInactive(this TeamMemberRoleFaker faker)
    {
        faker.RuleFor(x => x.IsActive, false);
        return faker;
    }
}
