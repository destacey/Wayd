using FluentAssertions;
using Wayd.Common.Domain.Authorization;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Who gets a sign-in, and what it can do. The point of the area is that signing in as different people
/// behaves differently, so the assertions are about the spread of roles rather than about counts.
/// </summary>
public class UserGeneratorTests
{
    private static readonly DateTime _asOf = DateTime.UtcNow.Date;

    private static (GeneratedOrg Org, GeneratedPpm Ppm) Generate()
    {
        var context = new GenerationContext { AsOf = _asOf, Seed = 1234 };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 15 }, context).Generate();
        var ppm = new PpmGenerator(org.Structure, new PpmOptions(), context).Generate();

        return (org, ppm);
    }

    private static IReadOnlyList<GeneratedUser> Users()
    {
        var (org, ppm) = Generate();

        return new UserGenerator(org, ppm).Generate();
    }

    [Fact]
    public void Generate_GivesEveryAccountAnEmployeeThatExists()
    {
        // Arrange — the whole reason the area exists: an account not linked to an employee cannot hold a
        // PPM role, so every delivery-leadership rule is unreachable from it
        var (org, ppm) = Generate();
        var employeeNumbers = org.Employees.Select(e => e.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = new UserGenerator(org, ppm).Generate()
            .Where(u => !employeeNumbers.Contains(u.EmployeeNumber))
            .ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_GivesEachPersonOneAccount()
    {
        // Arrange & Act — somebody who leads a project and sponsors a portfolio is still one person, and
        // a second account on the same email is rejected by the API
        var users = Users();

        // Assert
        users.Select(u => u.EmployeeNumber).Should().OnlyHaveUniqueItems();
        users.Select(u => u.Email).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_SpreadsAccountsAcrossSeveralRoles()
    {
        // Arrange & Act — an environment where every account can do the same things cannot demonstrate a
        // permission rule, which is the failure this area was built to remove
        var roles = Users().Select(u => u.RoleName).Distinct().ToList();

        // Assert
        roles.Should().HaveCountGreaterThan(3);
    }

    [Fact]
    public void Generate_KeepsTheMembershipBypassRare()
    {
        // Arrange — Administer PPM waives per-record delivery leadership across the whole domain. Handing
        // it out widely puts the environment back where it started: every account able to change anything,
        // and no way to see the membership rules working.
        var users = Users();

        // Act
        var unrestricted = users
            .Where(u => u.RoleName is SeedRoleCatalog.PpmAdministrator or ApplicationRoles.Admin)
            .ToList();

        // Assert
        unrestricted.Should().HaveCountLessThanOrEqualTo(2);
        users.Should().HaveCountGreaterThan(10, "the restricted accounts are the point, so there must be many more of them");
    }

    [Fact]
    public void Generate_GivesProjectLeadersARoleThatStillDependsOnLeadership()
    {
        // Arrange — a project owner holds Update Projects, which reaches the endpoint but not the record:
        // the domain still asks whether they lead that particular project. That pairing is what makes the
        // account useful for checking the rule.
        var (org, ppm) = Generate();
        var leaders = ppm.Projects
            .SelectMany(p => Split(p.Owners).Concat(Split(p.Managers)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var users = new UserGenerator(org, ppm).Generate()
            .Where(u => leaders.Contains(u.EmployeeNumber))
            .ToList();

        // Assert
        users.Should().NotBeEmpty();
        users.Should().OnlyContain(u => u.RoleName != SeedRoleCatalog.ReadOnly);
    }

    [Fact]
    public void Generate_StillProducesASignInWhenPpmWasSkipped()
    {
        // Arrange — an organization-only run has no PPM roles to draw from, and an environment nobody can
        // sign in to is not worth seeding
        var context = new GenerationContext { AsOf = _asOf, Seed = 1234 };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 2, Teams = 8 }, context).Generate();

        // Act
        var users = new UserGenerator(org, ppm: null).Generate();

        // Assert — the executives, who exist regardless of PPM
        users.Should().NotBeEmpty();
        users.Should().Contain(u => u.RoleName == ApplicationRoles.Admin);
    }

    private static IEnumerable<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
