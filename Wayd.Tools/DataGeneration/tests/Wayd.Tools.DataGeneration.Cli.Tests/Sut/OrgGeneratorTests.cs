using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class OrgGeneratorTests
{
    private static GeneratedOrg Generate(OrgOptions? options = null) =>
        new OrgGenerator(options ?? new OrgOptions { ValueStreams = 3, Teams = 20, Seed = 1234 }).Generate();

    [Fact]
    public void Generate_EveryManagerReferenceResolvesToAnEmployee()
    {
        // Arrange
        var org = Generate();
        var employeeNumbers = org.Employees.Select(e => e.EmployeeNumber).ToHashSet();

        // Act
        var danglingManagers = org.Employees
            .Where(e => e.ManagerNumber is not null && !employeeNumbers.Contains(e.ManagerNumber))
            .Select(e => e.ManagerNumber)
            .ToList();

        // Assert
        danglingManagers.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EmployeeNumbersAndEmailsAreUnique()
    {
        // Arrange
        var org = Generate();

        // Act / Assert
        org.Employees.Select(e => e.EmployeeNumber).Should().OnlyHaveUniqueItems();
        org.Employees.Select(e => e.Email).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_ExactlyOneEmployeeHasNoManager()
    {
        // Arrange — the CEO is the single root of the whole company.
        var org = Generate();

        // Act
        var roots = org.Employees.Where(e => e.ManagerNumber is null).ToList();

        // Assert
        roots.Should().ContainSingle();
        roots.Single().JobTitle.Should().Be("Chief Executive Officer");
    }

    // ---- Worker type rules --------------------------------------------------------------------

    [Fact]
    public void Generate_EveryManagerIsARegularEmployee()
    {
        // Arrange
        var org = Generate();
        var managerNumbers = org.Employees
            .Where(e => e.ManagerNumber is not null)
            .Select(e => e.ManagerNumber!)
            .ToHashSet();

        // Act
        var nonRegularManagers = org.Employees
            .Where(e => managerNumbers.Contains(e.EmployeeNumber) && e.EmployeeType != "Employee")
            .ToList();

        // Assert
        nonRegularManagers.Should().BeEmpty("a person who manages others is always a regular employee");
    }

    [Fact]
    public void Generate_TheVastMajorityAreRegularEmployees()
    {
        // Arrange
        var org = Generate();

        // Act
        var regularShare = org.Employees.Count(e => e.EmployeeType == "Employee") / (double)org.Employees.Count;

        // Assert
        regularShare.Should().BeGreaterThan(0.75);
    }

    [Fact]
    public void Generate_EveryEmployeeHasAWorkerType()
    {
        // Arrange
        var org = Generate();

        // Act / Assert
        org.Employees.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.EmployeeType));
    }

    // ---- Hierarchy validity -------------------------------------------------------------------

    [Fact]
    public void Generate_EveryMembershipReferencesExistingTeamsAndAParentTeamOfTeams()
    {
        // Arrange
        var org = Generate();
        var teamsByCode = org.Teams.ToDictionary(t => t.Code);

        // Act / Assert
        foreach (var link in org.TeamMemberships)
        {
            teamsByCode.Should().ContainKey(link.ChildCode);
            teamsByCode.Should().ContainKey(link.ParentCode);
            teamsByCode[link.ParentCode].Type.Should().Be("TeamOfTeams", "a parent must be a team of teams");
            link.ChildCode.Should().NotBe(link.ParentCode);
        }
    }

    [Fact]
    public void Generate_MembershipStartIsOnOrAfterBothTeamsActiveDates()
    {
        // Arrange — the domain import requires the membership to start no earlier than either team's active date.
        var org = Generate();
        var activeByCode = org.Teams.ToDictionary(t => t.Code, t => t.ActiveDate);

        // Act / Assert
        foreach (var link in org.TeamMemberships)
        {
            link.Start.Should().BeOnOrAfter(activeByCode[link.ChildCode]);
            link.Start.Should().BeOnOrAfter(activeByCode[link.ParentCode]);
        }
    }

    [Fact]
    public void Generate_TeamHierarchyIsAcyclic()
    {
        // Arrange
        var org = Generate();
        var parentOf = org.TeamMemberships.ToDictionary(m => m.ChildCode, m => m.ParentCode);

        // Act / Assert — walking parents from any node must terminate at a root (no cycle).
        foreach (var start in parentOf.Keys)
        {
            var seen = new HashSet<string>();
            var current = start;
            while (parentOf.TryGetValue(current, out var parent))
            {
                seen.Add(current).Should().BeTrue($"the hierarchy above '{start}' must not contain a cycle");
                current = parent;
            }
        }
    }

    [Fact]
    public void Generate_EachLeafTeamHasExactlyOneParent()
    {
        // Arrange
        var org = Generate();
        var leafTeamCodes = org.Teams.Where(t => t.Type == "Team").Select(t => t.Code);

        // Act
        var parentCounts = org.TeamMemberships
            .GroupBy(m => m.ChildCode)
            .ToDictionary(g => g.Key, g => g.Count());

        // Assert — every leaf team rolls up to exactly one ART.
        foreach (var code in leafTeamCodes)
            parentCounts.GetValueOrDefault(code).Should().Be(1, $"team '{code}' should have exactly one parent");
    }

    // ---- Staffing -----------------------------------------------------------------------------

    [Fact]
    public void Generate_EveryStaffingRowResolvesTeamEmployeeAndRole()
    {
        // Arrange
        var org = Generate();
        var teamCodes = org.Teams.Select(t => t.Code).ToHashSet();
        var employeeNumbers = org.Employees.Select(e => e.EmployeeNumber).ToHashSet();
        var roleNames = org.RoleNames.ToHashSet();

        // Act / Assert
        org.Members.Should().OnlyContain(m =>
            teamCodes.Contains(m.TeamCode) &&
            employeeNumbers.Contains(m.EmployeeNumber) &&
            roleNames.Contains(m.RoleName));
    }

    [Fact]
    public void Generate_OnlyActiveEmployeesAreStaffedOnTeams()
    {
        // Arrange — the domain import rejects inactive team members, so no staffed person may be inactive.
        var org = Generate();
        var inactiveNumbers = org.Employees.Where(e => !e.IsActive).Select(e => e.EmployeeNumber).ToHashSet();

        // Act
        var inactiveStaffed = org.Members.Where(m => inactiveNumbers.Contains(m.EmployeeNumber)).ToList();

        // Assert
        inactiveStaffed.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EachTeamHasAnEngineeringManagerWhoIsAlsoAnIndividualContributor()
    {
        // Arrange
        var org = Generate();
        var leafTeamCodes = org.Teams.Where(t => t.Type == "Team").Select(t => t.Code);

        // Act / Assert — the EM appears on the team twice: once as Engineering Manager, once as an IC discipline.
        foreach (var code in leafTeamCodes)
        {
            var teamRows = org.Members.Where(m => m.TeamCode == code).ToList();
            var em = teamRows.FirstOrDefault(m => m.RoleName == "Engineering Manager");
            em.Should().NotBeNull($"team '{code}' should have an engineering manager");

            var emRoles = teamRows.Where(m => m.EmployeeNumber == em!.EmployeeNumber).ToList();
            emRoles.Should().HaveCountGreaterThan(1, "the engineering manager is also an individual contributor on the team");
        }
    }

    // ---- Delivery ratio -----------------------------------------------------------------------

    [Theory]
    [InlineData(CompanyType.Tech, 0.85)]
    [InlineData(CompanyType.Enterprise, 0.2)]
    public void Generate_DeliveryRatioMatchesCompanyType(CompanyType companyType, double expected)
    {
        // Arrange
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, CompanyType = companyType, Seed = 1234 });

        // Act — "inside" = people staffed on a team.
        var staffedNumbers = org.Members.Select(m => m.EmployeeNumber).ToHashSet();
        var insideShare = staffedNumbers.Count / (double)org.Employees.Count;

        // Assert — within a tolerance band, since the outside population is sized by rounding.
        insideShare.Should().BeApproximately(expected, 0.08);
    }

    // ---- Reproducibility ----------------------------------------------------------------------

    [Fact]
    public void Generate_SameSeedProducesIdenticalOutput()
    {
        // Arrange
        var options = new OrgOptions { ValueStreams = 3, Teams = 20, Seed = 42 };

        // Act
        var a = new OrgGenerator(options).Generate();
        var b = new OrgGenerator(options).Generate();

        // Assert
        Serialize(a).Should().Be(Serialize(b));
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentOutput()
    {
        // Arrange / Act
        var a = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 20, Seed = 1 }).Generate();
        var b = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 20, Seed = 2 }).Generate();

        // Assert
        Serialize(a).Should().NotBe(Serialize(b));
    }

    private static string Serialize(GeneratedOrg org)
    {
        var employees = org.Employees.Select(e => $"{e.EmployeeNumber}|{e.Email}|{e.JobTitle}|{e.EmployeeType}|{e.ManagerNumber}|{e.IsActive}");
        var teams = org.Teams.Select(t => $"{t.Code}|{t.Type}|{t.Name}|{t.ActiveDate:O}");
        var links = org.TeamMemberships.Select(m => $"{m.ChildCode}->{m.ParentCode}|{m.Start:O}");
        var members = org.Members.Select(m => $"{m.TeamCode}|{m.EmployeeNumber}|{m.RoleName}");
        return string.Join("\n", employees.Concat(teams).Concat(links).Concat(members));
    }
}
