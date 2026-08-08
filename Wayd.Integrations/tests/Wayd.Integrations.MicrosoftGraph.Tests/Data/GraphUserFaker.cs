using Bogus;
using Microsoft.Graph.Models;

namespace Wayd.Integrations.MicrosoftGraph.Tests.Data;

/// <summary>
/// Builds Microsoft Graph <see cref="User"/> payloads for projection tests. Graph's models are
/// public-constructor POCOs, so this is a plain <see cref="Faker{T}"/> rather than the
/// <c>PrivateConstructorFaker</c> the domain entities need.
/// </summary>
/// <remarks>
/// Identity fields are randomized so no real person's name or address ends up in the suite. Assert
/// against the generated <see cref="User"/> (<c>user.Mail</c>, <c>user.GivenName</c>) rather than a
/// literal, and pin an explicit value with a <c>With*</c> extension only when the test is about
/// that specific value — all-caps names for the casing rules, a prefixed address for the
/// proxyAddresses parsing.
/// </remarks>
public sealed class GraphUserFaker : Faker<User>
{
    public GraphUserFaker()
    {
        RuleFor(u => u.Id, f => f.Random.Guid().ToString());
        RuleFor(u => u.GivenName, f => f.Name.FirstName());
        RuleFor(u => u.Surname, f => f.Name.LastName());
        RuleFor(u => u.Mail, f => f.Internet.Email());
        RuleFor(u => u.AccountEnabled, true);
        RuleFor(u => u.EmployeeId, (string?)null);
        RuleFor(u => u.ProxyAddresses, (List<string>?)null);
    }
}

public static class GraphUserFakerExtensions
{
    public static GraphUserFaker WithId(this GraphUserFaker faker, string id)
    {
        faker.RuleFor(u => u.Id, id);
        return faker;
    }

    public static GraphUserFaker WithGivenName(this GraphUserFaker faker, string givenName)
    {
        faker.RuleFor(u => u.GivenName, givenName);
        return faker;
    }

    public static GraphUserFaker WithSurname(this GraphUserFaker faker, string surname)
    {
        faker.RuleFor(u => u.Surname, surname);
        return faker;
    }

    public static GraphUserFaker WithName(this GraphUserFaker faker, string givenName, string surname)
    {
        faker.RuleFor(u => u.GivenName, givenName);
        faker.RuleFor(u => u.Surname, surname);
        return faker;
    }

    public static GraphUserFaker WithMail(this GraphUserFaker faker, string? mail)
    {
        faker.RuleFor(u => u.Mail, mail);
        return faker;
    }

    public static GraphUserFaker WithUserPrincipalName(this GraphUserFaker faker, string? userPrincipalName)
    {
        faker.RuleFor(u => u.UserPrincipalName, userPrincipalName);
        return faker;
    }

    public static GraphUserFaker WithEmployeeId(this GraphUserFaker faker, string? employeeId)
    {
        faker.RuleFor(u => u.EmployeeId, employeeId);
        return faker;
    }

    public static GraphUserFaker WithProxyAddresses(this GraphUserFaker faker, params string[] proxyAddresses)
    {
        faker.RuleFor(u => u.ProxyAddresses, [.. proxyAddresses]);
        return faker;
    }

    public static GraphUserFaker WithAccountEnabled(this GraphUserFaker faker, bool accountEnabled)
    {
        faker.RuleFor(u => u.AccountEnabled, accountEnabled);
        return faker;
    }
}
