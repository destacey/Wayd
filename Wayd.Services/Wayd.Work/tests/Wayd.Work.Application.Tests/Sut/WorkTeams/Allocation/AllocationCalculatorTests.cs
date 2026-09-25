using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Dtos;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Allocation;

public sealed class AllocationCalculatorTests
{
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 28);
    private static readonly LocalDate Early = new(2026, 7, 10);
    private static readonly LocalDate Late = new(2026, 9, 10);

    private static readonly PpmRecordReference PortfolioA = new(Guid.NewGuid(), 1, "Customer Experience");
    private static readonly PpmRecordReference PortfolioB = new(Guid.NewGuid(), 2, "Platform Modernization");
    private static readonly PpmRecordReference ProgramA = new(Guid.NewGuid(), 10, "Digital Onboarding");
    private static readonly PpmRecordReference ThemeGrow = new(Guid.NewGuid(), 20, "Grow self-serve revenue");
    private static readonly PpmRecordReference ThemeTrust = new(Guid.NewGuid(), 21, "Trust & compliance");

    private static readonly TeamStructureTeam Root = new(Guid.NewGuid(), 1, "ART", "Platform ART", TeamType.TeamOfTeams);
    private static readonly TeamStructureTeam CoreServices = new(Guid.NewGuid(), 2, "CORE", "Core Services", TeamType.TeamOfTeams);
    private static readonly TeamStructureTeam Experience = new(Guid.NewGuid(), 3, "EXP", "Experience", TeamType.TeamOfTeams);
    private static readonly TeamStructureTeam Payments = new(Guid.NewGuid(), 4, "PAY", "Payments Team", TeamType.Team);
    private static readonly TeamStructureTeam Mobile = new(Guid.NewGuid(), 5, "MOB", "Mobile Team", TeamType.Team);

    private static readonly WorkTeamNavigationDto RootNavigation = new()
    {
        Id = Root.Id,
        Key = Root.Key,
        Name = Root.Name,
        Code = Root.Code,
        Type = "Team of Teams",
    };

    private static readonly AllocationOptions ItemsByPortfolio = new(
        AllocationDimension.Portfolio, AllocationMeasure.Count, UnestimatedHandling.Exclude, ThemeCounting.SplitEvenly);

    private readonly Dictionary<Guid, ProjectClassification> _projects = [];

    /// <summary>Payments sizes in points and Mobile by count; both sit under Core Services the whole window.</summary>
    private static TeamStructure Structure(IEnumerable<TeamStructureMembership>? memberships = null) => new(
        Root.Id,
        [Root, CoreServices, Experience, Payments, Mobile],
        memberships?.ToList() ??
        [
            new(CoreServices.Id, Root.Id, From.PlusYears(-1), null),
            new(Experience.Id, Root.Id, From.PlusYears(-1), null),
            new(Payments.Id, CoreServices.Id, From.PlusYears(-1), null),
            new(Mobile.Id, CoreServices.Id, From.PlusYears(-1), null),
        ],
        [
            new(Payments.Id, From.PlusYears(-1), null, true),
            new(Mobile.Id, From.PlusYears(-1), null, false),
        ]);

    private ProjectClassification Project(string key, PpmRecordReference portfolio, PpmRecordReference? program = null, params PpmRecordReference[] themes)
    {
        var project = new ProjectClassification(Guid.NewGuid(), key, $"Project {key}", portfolio, program, themes, false);
        _projects[project.ProjectId] = project;
        return project;
    }

    private static AllocationWorkItem Item(TeamStructureTeam team, ProjectClassification? project = null, double? points = null, LocalDate? doneOn = null, string type = "User Story") =>
        new(Guid.NewGuid(), team.Id, type, doneOn ?? Early, project?.ProjectId, points);

    private TeamAllocationDto Calculate(IReadOnlyList<AllocationWorkItem> items, AllocationOptions? options = null, TeamStructure? structure = null) =>
        AllocationCalculator.Calculate(RootNavigation, From, To, structure ?? Structure(), items, _projects, options ?? ItemsByPortfolio);

    [Fact]
    public void Calculate_ByPortfolio_PutsNoProjectLastWithSharesOfTheTotal()
    {
        // Arrange
        var small = Project("SMALL", PortfolioA);
        var large = Project("LARGE", PortfolioB);
        List<AllocationWorkItem> items = [Item(Payments, small), Item(Payments, large), Item(Payments, large), Item(Payments)];

        // Act
        var result = Calculate(items);

        // Assert
        result.Groups.Select(g => g.Name).Should().Equal("Platform Modernization", "Customer Experience", "No project");
        result.Groups.Select(g => g.Share).Should().Equal(50, 25, 25);
        result.Groups[^1].Kind.Should().Be(AllocationGroupKind.NoProject);
        result.Summary.ItemsCompleted.Should().Be(4);
        result.Summary.NoProjectItems.Should().Be(1);
        result.Summary.NoProjectShare.Should().Be(25);
    }

    [Fact]
    public void Calculate_TeamOfTeams_RollsWorkUpEveryLevel()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, project), Item(Mobile, project), Item(Mobile)];

        // Act
        var result = Calculate(items);

        // Assert
        result.Teams.Select(r => (r.Name, r.Level)).Should().Equal(
            ("Platform ART", 0), ("Core Services", 1), ("Mobile Team", 2), ("Payments Team", 2), ("Experience", 1));
        result.Teams.Single(r => r.TeamId == Root.Id).Items.Should().Be(3);
        result.Teams.Single(r => r.TeamId == CoreServices.Id).Items.Should().Be(3);
        result.Teams.Single(r => r.TeamId == Mobile.Id).Cells.Select(c => c.Share).Should().Equal(50, 50);
    }

    [Fact]
    public void Calculate_TeamThatMoved_RollsUpToItsParentOnTheDayTheWorkWasDone()
    {
        // Arrange
        var moveDate = new LocalDate(2026, 8, 15);
        var structure = Structure(
        [
            new(CoreServices.Id, Root.Id, From.PlusYears(-1), null),
            new(Experience.Id, Root.Id, From.PlusYears(-1), null),
            new(Payments.Id, CoreServices.Id, From.PlusYears(-1), null),
            new(Mobile.Id, CoreServices.Id, From.PlusYears(-1), moveDate.PlusDays(-1)),
            new(Mobile.Id, Experience.Id, moveDate, null),
        ]);
        List<AllocationWorkItem> items = [Item(Mobile, doneOn: Early), Item(Mobile, doneOn: Late), Item(Mobile, doneOn: Late)];

        // Act
        var result = Calculate(items, structure: structure);

        // Assert
        result.Teams.Single(r => r.TeamId == CoreServices.Id).Items.Should().Be(1);
        result.Teams.Single(r => r.TeamId == Experience.Id).Items.Should().Be(2);
        result.Teams.Single(r => r.TeamId == Mobile.Id).ParentId.Should().Be(Experience.Id);
    }

    [Fact]
    public void Calculate_WorkDoneOutsideTheWindow_IsLeftOut()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items =
        [
            Item(Payments, project, doneOn: From.PlusDays(-1)),
            Item(Payments, project, doneOn: From),
            Item(Payments, project, doneOn: To),
            Item(Payments, project, doneOn: To.PlusDays(1)),
        ];

        // Act
        var result = Calculate(items);

        // Assert
        result.Summary.ItemsCompleted.Should().Be(2);
        result.Periods.Sum(p => p.Items).Should().Be(2);
    }

    [Fact]
    public void Calculate_WorkDoneWhileOutsideTheHierarchy_IsLeftOut()
    {
        // Arrange
        var joined = new LocalDate(2026, 8, 1);
        var structure = Structure(
        [
            new(CoreServices.Id, Root.Id, From.PlusYears(-1), null),
            new(Payments.Id, CoreServices.Id, joined, null),
        ]);
        List<AllocationWorkItem> items = [Item(Payments, doneOn: Early), Item(Payments, doneOn: Late)];

        // Act
        var result = Calculate(items, structure: structure);

        // Assert
        result.Summary.ItemsCompleted.Should().Be(1);
    }

    [Fact]
    public void Calculate_ByProgram_SeparatesAProjectWithNoProgramFromNoProject()
    {
        // Arrange
        var inProgram = Project("IN", PortfolioA, ProgramA);
        var direct = Project("DIRECT", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, inProgram), Item(Payments, direct), Item(Payments)];

        // Act
        var result = Calculate(items, ItemsByPortfolio with { Dimension = AllocationDimension.Program });

        // Assert
        result.Groups.Select(g => (g.Name, g.Kind)).Should().Equal(
            ("Digital Onboarding", AllocationGroupKind.Record),
            ("No program", AllocationGroupKind.MissingLevel),
            ("No project", AllocationGroupKind.NoProject));
        result.Groups[1].ProjectKeys.Should().Equal("DIRECT");
        result.Groups[0].Portfolio!.Name.Should().Be(PortfolioA.Name);
    }

    [Fact]
    public void Calculate_ThemesSplitEvenly_AddsUpToAHundred()
    {
        // Arrange
        var both = Project("BOTH", PortfolioA, null, ThemeGrow, ThemeTrust);
        var grow = Project("GROW", PortfolioA, null, ThemeGrow);
        List<AllocationWorkItem> items = [Item(Payments, both), Item(Payments, grow)];

        // Act
        var result = Calculate(items, ItemsByPortfolio with { Dimension = AllocationDimension.StrategicTheme });

        // Assert
        result.Groups.Select(g => (g.Name, g.Items)).Should().Equal((ThemeGrow.Name, 1.5), (ThemeTrust.Name, 0.5));
        result.Groups.Sum(g => g.Share).Should().Be(100);
    }

    [Fact]
    public void Calculate_ThemesCountedFully_CreditsEachThemeInFull()
    {
        // Arrange
        var both = Project("BOTH", PortfolioA, null, ThemeGrow, ThemeTrust);
        var grow = Project("GROW", PortfolioA, null, ThemeGrow);
        List<AllocationWorkItem> items = [Item(Payments, both), Item(Payments, grow)];
        var options = ItemsByPortfolio with { Dimension = AllocationDimension.StrategicTheme, ThemeCounting = ThemeCounting.CountFully };

        // Act
        var result = Calculate(items, options);

        // Assert
        result.Groups.Select(g => (g.Name, g.Share)).Should().Equal((ThemeGrow.Name, 100.0), (ThemeTrust.Name, 50.0));
    }

    [Fact]
    public void Calculate_ProjectWithoutThemes_IsNoTheme()
    {
        // Arrange
        var bare = Project("BARE", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, bare), Item(Payments)];

        // Act
        var result = Calculate(items, ItemsByPortfolio with { Dimension = AllocationDimension.StrategicTheme });

        // Assert
        result.Groups.Select(g => (g.Name, g.Kind)).Should().Equal(
            ("No theme", AllocationGroupKind.MissingLevel),
            ("No project", AllocationGroupKind.NoProject));
    }

    [Fact]
    public void Calculate_StoryPoints_LeavesOutCountSizedTeamsAndUnestimatedItems()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, project, 5), Item(Payments, project), Item(Mobile, project)];
        var options = ItemsByPortfolio with { Measure = AllocationMeasure.StoryPoints };

        // Act
        var result = Calculate(items, options);

        // Assert
        result.Groups.Should().ContainSingle().Which.Value.Should().Be(5);
        result.Summary.ItemsInPointSizedTeams.Should().Be(2);
        result.Summary.EstimatedItems.Should().Be(1);
        result.Summary.ExcludedTeams.Select(t => t.Id).Should().Equal(Mobile.Id);
        result.Teams.Single(r => r.TeamId == Mobile.Id).Excluded.Should().BeTrue();
        result.Teams.Single(r => r.TeamId == CoreServices.Id).Excluded.Should().BeFalse();
    }

    [Fact]
    public void Calculate_TeamAverage_FillsFromTheSameTeamAndWorkType()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items =
        [
            Item(Payments, project, 8),
            Item(Payments, project, 1, type: "Bug"),
            Item(Payments, project, 3, type: "Bug"),
            Item(Payments, project, type: "Bug"),
        ];
        var options = ItemsByPortfolio with { Measure = AllocationMeasure.StoryPoints, Unestimated = UnestimatedHandling.TeamAverage };

        // Act
        var result = Calculate(items, options);

        // Assert
        result.Summary.StoryPoints.Should().Be(12);
        result.Summary.FilledItems.Should().Be(1);
        result.Summary.FilledStoryPoints.Should().Be(2);
        result.Groups.Single().Value.Should().Be(14);
        result.Groups.Single().FilledStoryPoints.Should().Be(2);
    }

    [Fact]
    public void Calculate_TeamEffort_WeighsEachTeamByItsShareOfItems()
    {
        // Arrange
        var x = Project("X", PortfolioA);
        var y = Project("Y", PortfolioB);
        List<AllocationWorkItem> items = [Item(Payments, x, 1), Item(Payments, y, 3), Item(Mobile, y), Item(Mobile, y)];
        var options = ItemsByPortfolio with { Measure = AllocationMeasure.TeamEffort };

        // Act
        var result = Calculate(items, options);

        // Assert
        result.Groups.Select(g => (g.Name, g.Share)).Should().Equal((PortfolioB.Name, 87.5), (PortfolioA.Name, 12.5));
        result.Summary.ExcludedTeams.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_ByWorkType_ReportsTheNoProjectPartOfEachType()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items =
        [
            Item(Payments, project, type: "Bug"),
            Item(Payments, type: "Bug"),
            Item(Payments, type: "bug "),
            Item(Payments, project),
        ];

        // Act
        var result = Calculate(items, ItemsByPortfolio with { Dimension = AllocationDimension.WorkType });

        // Assert
        var bug = result.Groups.First();
        bug.Name.Should().Be("Bug");
        bug.Items.Should().Be(3);
        bug.NoProjectShare.Should().BeApproximately(66.67, 0.01);
        result.Groups.Should().NotContain(g => g.Kind == AllocationGroupKind.NoProject);
    }

    [Fact]
    public void Calculate_ThreeMonthWindow_BucketsInTwoWeeks()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, project, doneOn: From), Item(Payments, doneOn: To)];

        // Act
        var result = Calculate(items);

        // Assert
        result.Periods.Should().HaveCount(7);
        result.Periods[0].Should().Match<AllocationPeriodDto>(p => p.Start == From && p.End == From.PlusDays(13) && p.Items == 1);
        result.Periods[^1].End.Should().Be(To);
        result.Periods[^1].NoProjectShare.Should().Be(100);
        result.Periods[0].Shares.Should().Equal(100, 0);
    }

    [Fact]
    public void Calculate_NamesTheTeamThatDidTheMostInEachGroup()
    {
        // Arrange
        var project = Project("ONE", PortfolioA);
        List<AllocationWorkItem> items = [Item(Payments, project), Item(Mobile, project), Item(Mobile, project)];

        // Act
        var result = Calculate(items);

        // Assert
        result.Groups.Single().LargestContributor.Should().Be(new AllocationContributorDto(Mobile.Id, Mobile.Code, Mobile.Name, 2, 2));
    }
}
