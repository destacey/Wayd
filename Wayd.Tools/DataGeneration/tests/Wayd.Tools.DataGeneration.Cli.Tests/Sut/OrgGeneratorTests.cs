using FluentAssertions;
using Wayd.Common.Models;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class OrgGeneratorTests
{
    /// <summary>A pinned context, so a test never depends on the day it runs on.</summary>
    private static GenerationContext Context(int seed = 1234) =>
        new() { AsOf = new DateOnly(2026, 6, 15), Seed = seed };

    private static GeneratedOrg Generate(OrgOptions? options = null, GenerationContext? context = null) =>
        new OrgGenerator(options ?? new OrgOptions { ValueStreams = 3, Teams = 20 }, context ?? Context()).Generate();

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
    public void Generate_ExactlyOneActiveEmployeeHasNoManager()
    {
        // Arrange — the CEO is the single root of the whole company. Former CEOs had no manager either.
        var org = Generate();

        // Act
        var roots = org.Employees.Where(e => e.IsActive && e.ManagerNumber is null).ToList();

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
    public void Generate_NoStaffingRowIsEmittedTwice()
    {
        // Arrange
        // The same person, team and role stated twice is the same fact twice. The import rejects it — each
        // row's import id must be unique within a file — and before that check existed it silently
        // collapsed the pair, so the seeded data quietly lost a row instead of failing.
        var org = Generate();

        // Act
        var duplicated = org.Members
            .GroupBy(m => (m.TeamCode, m.EmployeeNumber, m.RoleName))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Assert
        duplicated.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ImportIdsAreUniqueWithinEveryFile()
    {
        // Arrange
        // Every import rejects a file whose import ids repeat, so a collision fails the seed rather than
        // any one row.
        var org = Generate();

        // Act & Assert
        org.Employees.Select(e => e.ImportId).Should().OnlyHaveUniqueItems();
        org.Teams.Select(t => t.ImportId).Should().OnlyHaveUniqueItems();
        org.TeamMemberships.Select(m => m.ImportId).Should().OnlyHaveUniqueItems();
        org.Members.Select(m => m.ImportId).Should().OnlyHaveUniqueItems();
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
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, CompanyType = companyType });

        // Act — "inside" = people staffed on a team, as a share of the company today. Leavers are history,
        // not headcount.
        var staffedNumbers = org.Members.Select(m => m.EmployeeNumber).ToHashSet();
        var insideShare = staffedNumbers.Count / (double)org.Employees.Count(e => e.IsActive);

        // Assert — within a tolerance band, since the outside population is sized by rounding.
        insideShare.Should().BeApproximately(expected, 0.08);
    }

    // ---- Reproducibility ----------------------------------------------------------------------

    [Fact]
    public void Generate_SameSeedAndAsOfProduceIdenticalOutput()
    {
        // Arrange
        var options = new OrgOptions { ValueStreams = 3, Teams = 20 };

        // Act
        var a = new OrgGenerator(options, Context(42)).Generate();
        var b = new OrgGenerator(options, Context(42)).Generate();

        // Assert
        Serialize(a).Should().Be(Serialize(b));
    }

    [Fact]
    public void Generate_DifferentSeedsProduceDifferentOutput()
    {
        // Arrange / Act
        var options = new OrgOptions { ValueStreams = 3, Teams = 20 };
        var a = new OrgGenerator(options, Context(1)).Generate();
        var b = new OrgGenerator(options, Context(2)).Generate();

        // Assert
        Serialize(a).Should().NotBe(Serialize(b));
    }

    [Fact]
    public void Generate_AnchorsDatesOnAsOfRatherThanTheRealToday()
    {
        // Arrange — the bug this replaced: dates were measured back from Bogus's own DateTime.Now, so a
        // pinned seed still produced a different dataset tomorrow while the tool claimed otherwise. A test
        // cannot stage two different days in one process, so it pins asOf far enough into the past that
        // anything still reading the real clock lands outside the window and is caught.
        var asOf = new DateOnly(2020, 6, 15);
        var context = new GenerationContext { AsOf = asOf, Seed = 42 };

        // Act
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 20 }, context).Generate();

        // Assert — nobody was hired after the run's today, and nobody before the company existed
        org.Employees.Should().OnlyContain(e => e.HireDate <= asOf.ToDateTime(TimeOnly.MinValue)
            && e.HireDate >= context.FoundedOn.ToDateTime(TimeOnly.MinValue));
        org.Teams.Should().OnlyContain(t => t.ActiveDate <= asOf && t.ActiveDate >= context.FoundedOn);
    }

    [Fact]
    public void Generate_MovingAsOfMovesTheWholeTimeline()
    {
        // Arrange — the other half: asOf is what anchors the data, so changing it has to change the dates
        var options = new OrgOptions { ValueStreams = 3, Teams = 20 };

        // Act
        var a = new OrgGenerator(options, new GenerationContext { AsOf = new DateOnly(2026, 6, 15), Seed = 42 }).Generate();
        var b = new OrgGenerator(options, new GenerationContext { AsOf = new DateOnly(2020, 6, 15), Seed = 42 }).Generate();

        // Assert
        a.Employees.Max(e => e.HireDate).Should().BeAfter(b.Employees.Max(e => e.HireDate)!.Value);
    }

    [Fact]
    public void Generate_ProducesEmailAddressesThatCanBecomeUsernames()
    {
        // Arrange — Wayd signs people in by email, so a generated address has to be one the username rule
        // accepts. Asserted against the same constant the API validates with rather than a copy of the
        // grammar, so a name that cannot become an address fails here rather than as a rejected account a
        // long way downstream.
        var org = Generate();

        // Act
        var unusable = org.Employees
            .Where(e => e.Email.Any(c => !EmailAddress.AllowedCharacters.Contains(c)))
            .Select(e => e.Email)
            .ToList();

        // Assert
        unusable.Should().BeEmpty();
    }

    [Fact]
    public void Generate_NoDatePrecedesTheCompanysFounding()
    {
        // Arrange — the floor. Employees span the full company age and teams a shorter one, but nothing
        // may fall before the company existed.
        var context = Context();

        // Act
        var org = Generate(context: context);

        // Assert
        org.Employees.Should().OnlyContain(e => e.HireDate >= context.FoundedOn.ToDateTime(TimeOnly.MinValue));
        org.Teams.Should().OnlyContain(t => t.ActiveDate >= context.FoundedOn);
    }

    [Fact]
    public void Generate_NoTeamMembershipStartsBeforeEitherTeamExisted()
    {
        // Arrange — no child may predate its parent, which is the floor applied one layer down
        var org = Generate();
        var activeByCode = org.Teams.ToDictionary(t => t.Code, t => t.ActiveDate);

        // Act
        var early = org.TeamMemberships
            .Where(m => m.Start < activeByCode[m.ChildCode] || m.Start < activeByCode[m.ParentCode])
            .Select(m => $"{m.ChildCode} under {m.ParentCode}")
            .ToList();

        // Assert
        early.Should().BeEmpty();
    }

    // ---- Structure switches -------------------------------------------------------------------

    [Fact]
    public void Generate_WithArtsOff_PutsTeamsDirectlyUnderTheirValueStream()
    {
        // Arrange — three value streams of about seven teams, each large enough for a top team
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, ArtTier = StructureMode.Off });
        var teamsByCode = org.Teams.ToDictionary(t => t.Code);
        var valueStreamCodes = org.Structure.ValueStreams.Select(v => v.TeamCode).ToHashSet();

        // Act
        var parents = org.TeamMemberships.Select(m => m.ParentCode).ToHashSet();

        // Assert
        org.Teams.Count(t => t.Type == "TeamOfTeams").Should().Be(3);
        parents.Should().BeSubsetOf(valueStreamCodes!);
        org.TeamMemberships.Should().OnlyContain(m => teamsByCode[m.ChildCode].Type == "Team");
        org.Structure.ValueStreams.Should().OnlyContain(v => v.Arts.Count == 1 && v.Arts[0].TeamCode == null);
        org.Members.Should().NotContain(m => m.RoleName == "RTE");
    }

    [Fact]
    public void Generate_WithBothTiersOff_LeavesTeamsFlatAndReportingToTheExecutives()
    {
        // Arrange
        var org = Generate(new OrgOptions { ValueStreams = 2, Teams = 8, ValueStreamTier = StructureMode.Off, ArtTier = StructureMode.Off });
        // Act
        var managersOfEms = org.Employees
            .Where(e => e.IsActive && e.JobTitle == "Engineering Manager")
            .Select(e => e.ManagerNumber)
            .ToHashSet();

        // Assert
        org.Teams.Should().OnlyContain(t => t.Type == "Team");
        org.TeamMemberships.Should().BeEmpty();
        managersOfEms.Should().BeEquivalentTo([org.Structure.ChiefTechnologyEmployeeNumber]);
    }

    [Fact]
    public void Generate_WithTheValueStreamTierOn_GivesEvenASmallValueStreamATopTeam()
    {
        // Arrange — three teams make one ART, which Auto would leave without a value stream above it
        var org = Generate(new OrgOptions { ValueStreams = 1, Teams = 3, ValueStreamTier = StructureMode.On });
        var valueStream = org.Structure.ValueStreams.Single();

        // Act
        var art = valueStream.Arts.Single();

        // Assert
        valueStream.TeamCode.Should().NotBeNull();
        valueStream.EngineeringLeadEmployeeNumber.Should().NotBeNull();
        org.TeamMemberships.Should().Contain(m => m.ChildCode == art.TeamCode && m.ParentCode == valueStream.TeamCode);
    }

    [Fact]
    public void Generate_WithTheValueStreamTierOff_LeavesArtsAtTheTopWithDistinctNames()
    {
        // Arrange — twelve teams make four ARTs, which Auto would put under a value stream
        var org = Generate(new OrgOptions { ValueStreams = 1, Teams = 12, ValueStreamTier = StructureMode.Off });
        var arts = org.Structure.ValueStreams.Single().Arts;

        // Act
        var artsWithAParent = org.TeamMemberships.Where(m => arts.Any(a => a.TeamCode == m.ChildCode)).ToList();

        // Assert
        arts.Should().HaveCount(4);
        artsWithAParent.Should().BeEmpty();
        arts.Select(a => a.Name).Should().OnlyHaveUniqueItems();
        org.Structure.ValueStreams.Single().TeamCode.Should().BeNull();
    }

    [Fact]
    public void Generate_OnAuto_MatchesTheSizeDerivedShape()
    {
        // Arrange — Auto is what a run did before the switches existed, and a pinned seed depends on it
        var options = new OrgOptions { ValueStreams = 3, Teams = 20 };
        var explicitAuto = new OrgOptions { ValueStreams = 3, Teams = 20, ValueStreamTier = StructureMode.Auto, ArtTier = StructureMode.Auto };

        // Act
        var a = Generate(options);
        var b = Generate(explicitAuto);

        // Assert
        Serialize(b).Should().Be(Serialize(a));
        a.Structure.ValueStreams.Should().OnlyContain(v => v.TeamCode != null && v.Arts.All(art => art.TeamCode != null));
    }

    // ---- Attrition ----------------------------------------------------------------------------

    [Fact]
    public void Generate_ReplacesEveryLeaver()
    {
        // Arrange — attrition adds history; it must not shrink or reshape the company as it stands today
        var context = Context();

        // Act
        var without = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0 }, context);
        var with = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 }, context);

        // Assert
        with.Employees.Count(e => e.IsActive).Should().Be(without.Employees.Count);
        with.Employees.Should().Contain(e => !e.IsActive);
        with.Members.Select(m => m.ImportId).Should().Equal(without.Members.Select(m => m.ImportId));
    }

    [Fact]
    public void Generate_WithNoAttrition_HasNoFormerEmployees()
    {
        // Arrange & Act
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0 });

        // Assert
        org.Employees.Should().OnlyContain(e => e.IsActive);
    }

    [Fact]
    public void Generate_LosesPeopleAtAboutTheAnnualRateOverTheHistory()
    {
        // Arrange — two years of history at 15% a year is about 0.3 leavers a position; the tenure floor and
        // the backfill lag trim the window a little, so the band sits below that
        var context = Context();
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 30, AttritionRate = 0.15 }, context);
        var positions = org.Employees.Count(e => e.IsActive);

        // Act
        var perPosition = org.Employees.Count(e => !e.IsActive) / (double)positions;

        // Assert
        perPosition.Should().BeInRange(0.15 * context.HistoryYears * 0.6, 0.15 * context.HistoryYears * 1.2);
    }

    [Fact]
    public void Generate_ReachesDeliveryTeamsAndManagers()
    {
        // Arrange — the flat fraction this replaced could only touch non-delivery individual contributors
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 });
        var leavers = org.Employees.Where(e => !e.IsActive).ToList();
        var managers = org.Employees.Where(e => e.ManagerNumber != null).Select(e => e.ManagerNumber).ToHashSet();

        // Act & Assert
        leavers.Should().Contain(e => e.JobTitle == "Engineering Manager");
        leavers.Should().Contain(e => e.Department == "Product");
        leavers.Should().Contain(e => managers.Contains(e.EmployeeNumber));
    }

    [Fact]
    public void Generate_GivesNoLeaverAnActiveReport()
    {
        // Arrange — a leaver can have managed people who have also since left, but today everyone reports
        // to someone still here
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 });
        var inactive = org.Employees.Where(e => !e.IsActive).Select(e => e.EmployeeNumber).ToHashSet();

        // Act
        var reportingToALeaver = org.Employees.Where(e => e.IsActive && e.ManagerNumber != null && inactive.Contains(e.ManagerNumber)).ToList();

        // Assert
        reportingToALeaver.Should().BeEmpty();
    }

    [Fact]
    public void Generate_HandsEachPositionOnWithoutOverlapOrGaps()
    {
        // Arrange
        var context = Context();
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 }, context);

        // Act
        var positions = org.Structure.Positions!.Values.Distinct().ToList();

        // Assert — one holder at a time, the replacement starting within weeks, and nobody outside the company's life
        positions.Should().Contain(p => p.Count > 1);
        foreach (var holders in positions)
        {
            holders[^1].LeftOn.Should().BeNull();
            holders.Take(holders.Count - 1).Where(h => h.LeftOn == null || h.LeftOn <= h.HiredOn).Should().BeEmpty();

            foreach (var (leaver, successor) in holders.Zip(holders.Skip(1)))
            {
                successor.HiredOn.Should().BeAfter(leaver.LeftOn!.Value);
                (successor.HiredOn.DayNumber - leaver.LeftOn!.Value.DayNumber).Should().BeLessThanOrEqualTo(45);
            }

            holders.Should().OnlyContain(h => h.HiredOn >= context.FoundedOn && h.HiredOn <= context.AsOf);
        }
    }

    [Fact]
    public void Generate_MarksExactlyThePeopleWhoLeftInactive()
    {
        // Arrange
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 });
        var tenures = org.Structure.Positions!;

        // Act & Assert
        org.Employees.Should().OnlyContain(e => e.IsActive == (tenures[e.EmployeeNumber].Single(t => t.EmployeeNumber == e.EmployeeNumber).LeftOn == null));
    }

    [Fact]
    public void Generate_StaffsDeliveryPositionsFromTheStartOfTheHistory()
    {
        // Arrange — history names whoever held a position on its own dates, so every delivery position has to
        // have been held by then, or old work would name someone hired after it finished
        var context = Context();
        var org = Generate(new OrgOptions { ValueStreams = 3, Teams = 20, AttritionRate = 0.2 }, context);
        var staffed = org.Members.Select(m => m.EmployeeNumber).ToHashSet();

        // Act
        var lateFirstHolders = org.Structure.Positions!
            .Where(p => staffed.Contains(p.Key))
            .Select(p => p.Value[0])
            .Where(first => first.HiredOn > context.WindowStart)
            .ToList();

        // Assert
        lateFirstHolders.Should().BeEmpty();
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
