using Wayd.Common.Domain.Identity;
using Wayd.TestData.Core;

namespace Wayd.Tests.Shared.Data;

/// <summary>
/// Produces <see cref="User"/> instances for tests.
/// </summary>
/// <remarks>
/// Cross-cutting rather than owned by one module's test project: <c>User</c> is the account record every
/// area attributes work to, so it is reached from Common and from the service test projects alike.
/// <para>
/// <c>Id</c> is a string, matching the column every feature stores an actor against.
/// </para>
/// </remarks>
public sealed class UserFaker : PrivateConstructorFaker<User>
{
    public UserFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid().ToString());
        RuleFor(x => x.UserName, f => f.Internet.UserName());
        RuleFor(x => x.FirstName, f => f.Name.FirstName());
        RuleFor(x => x.LastName, f => f.Name.LastName());
        RuleFor(x => x.DisplayName, (f, u) => $"{u.FirstName} {u.LastName}");
        RuleFor(x => x.Email, f => f.Internet.Email());
        RuleFor(x => x.IsActive, true);
    }
}

public static class UserFakerExtensions
{
    public static UserFaker WithId(this UserFaker faker, string id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static UserFaker WithUserName(this UserFaker faker, string userName)
    {
        faker.RuleFor(x => x.UserName, userName);
        return faker;
    }

    public static UserFaker WithDisplayName(this UserFaker faker, string? displayName)
    {
        faker.RuleFor(x => x.DisplayName, displayName);
        return faker;
    }

    public static UserFaker AsInactive(this UserFaker faker)
    {
        faker.RuleFor(x => x.IsActive, false);
        return faker;
    }
}
