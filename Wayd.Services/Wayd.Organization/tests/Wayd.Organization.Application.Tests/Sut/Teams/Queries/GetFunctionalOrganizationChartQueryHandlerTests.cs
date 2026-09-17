using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetFunctionalOrganizationChartQueryHandlerTests
{
    private static readonly LocalDate AsOf = new(2026, 6, 1);
    private static readonly LocalDate LastYear = AsOf.PlusYears(-1);
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetFunctionalOrganizationChartQueryHandler _handler;

    public GetFunctionalOrganizationChartQueryHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Today).Returns(AsOf);

        _handler = new GetFunctionalOrganizationChartQueryHandler(
            _dbContext,
            new Mock<ILogger<GetFunctionalOrganizationChartQueryHandler>>().Object,
            dateTimeProvider.Object);
    }

    [Fact]
    public async Task Handle_BuildsTheHierarchyFromTheMembershipsActiveOnTheDate()
    {
        // Arrange — VS ← ART ← (TEAM1, TEAM2), and SOLO reports to nobody
        var valueStream = AddTeamOfTeams("Payments VS", "VS");
        var art = AddTeamOfTeams("Payments ART", "ART");
        var team1 = AddTeam("Cards", "TEAM1");
        var team2 = AddTeam("Wallets", "TEAM2");
        AddTeam("Solo", "SOLO");
        Place(art, valueStream);
        Place(team1, art);
        Place(team2, art);

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.AsOfDate.Should().Be(AsOf);
        result.Total.Should().Be(5);
        result.MaxDepth.Should().Be(2);
        result.Organization.Select(u => u.Code).Should().Equal("VS", "SOLO");

        var vs = result.Organization.Single(u => u.Code == "VS");
        vs.Level.Should().Be(0);
        vs.Path.Should().Be("VS");
        vs.Type.Id.Should().Be((int)TeamType.TeamOfTeams);

        var artUnit = vs.Children.Should().ContainSingle().Subject;
        artUnit.Id.Should().Be(art.Id);
        artUnit.Level.Should().Be(1);
        artUnit.Path.Should().Be("VS -> ART");

        artUnit.Children.Should().HaveCount(2);
        artUnit.Children!.Select(u => u.Code).Should().Equal("TEAM1", "TEAM2");
        artUnit.Children.Should().AllSatisfy(u =>
        {
            u.Level.Should().Be(2);
            u.Path.Should().StartWith("VS -> ART -> ");
            u.Type.Id.Should().Be((int)TeamType.Team);
            u.Children.Should().BeNull();
        });

        result.Organization.Single(u => u.Code == "SOLO").Children.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TreatsATeamWhoseMembershipHasEndedAsARoot()
    {
        // Arrange — TEAM left ART at the end of last year
        var art = AddTeamOfTeams("Payments ART", "ART");
        var team = AddTeam("Cards", "TEAM");
        Place(team, art, start: LastYear, end: new LocalDate(2025, 12, 31));

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(2);
        result.MaxDepth.Should().Be(0);
        result.Organization.Select(u => u.Code).Should().Equal("TEAM", "ART");
        result.Organization.Should().AllSatisfy(u => u.Children.Should().BeNull());
    }

    [Fact]
    public async Task Handle_TreatsATeamWhoseMembershipHasNotStartedAsARoot()
    {
        // Arrange — TEAM joins ART after the date
        var art = AddTeamOfTeams("Payments ART", "ART");
        var team = AddTeam("Cards", "TEAM");
        Place(team, art, start: AsOf.PlusDays(1));

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(2);
        result.Organization.Select(u => u.Code).Should().Equal("TEAM", "ART");
    }

    [Fact]
    public async Task Handle_ExcludesATeamNotYetActiveOnTheDate()
    {
        // Arrange
        AddTeam("Cards", "TEAM");
        AddTeam("Future", "NEXT", activeDate: AsOf.PlusDays(1));

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(1);
        result.Organization.Select(u => u.Code).Should().Equal("TEAM");
    }

    [Fact]
    public async Task Handle_ExcludesATeamInactiveOnTheDate()
    {
        // Arrange
        AddTeam("Cards", "TEAM");
        var retired = new TeamFaker().WithCode(new TeamCode("OLD")).WithActiveDate(LastYear)
            .WithInactiveDate(AsOf.PlusDays(-1)).WithIsActive(false).Generate();
        _dbContext.AddTeam(retired);

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(1);
        result.Organization.Select(u => u.Code).Should().Equal("TEAM");
    }

    [Fact]
    public async Task Handle_PromotesTheChildOfAnInactiveParentToARoot()
    {
        // Arrange — the domain refuses to deactivate a parent over an open membership, so the state is
        // built directly: what the chart does with it is the point
        var parent = new TeamOfTeamsFaker().WithCode(new TeamCode("GONE")).WithActiveDate(LastYear)
            .WithInactiveDate(AsOf.PlusDays(-1)).WithIsActive(false).Generate();
        _dbContext.AddTeamOfTeams(parent);
        var team = AddTeam("Cards", "TEAM");
        Link(team, parent);

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(1);
        var unit = result.Organization.Should().ContainSingle().Subject;
        unit.Code.Should().Be("TEAM");
        unit.Level.Should().Be(0);
        unit.Path.Should().Be("TEAM");
    }

    [Fact]
    public async Task Handle_IgnoresSoftDeletedTeamsAndMemberships()
    {
        // Arrange — a deleted team, and a live team whose only membership is deleted
        var art = AddTeamOfTeams("Payments ART", "ART");
        var team = AddTeam("Cards", "TEAM");
        Link(team, art, isDeleted: true);
        AddTeam("Deleted", "DEL").IsDeleted = true;

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(2);
        result.Organization.Select(u => u.Code).Should().Equal("TEAM", "ART");
        result.Organization.Should().AllSatisfy(u => u.Children.Should().BeNull());
    }

    [Fact]
    public async Task Handle_PlacesTheMembersOfACycleRatherThanDroppingThem()
    {
        // Arrange — A under B and B under A; the domain forbids it, so it is built directly
        var a = AddTeamOfTeams("Alpha", "AA");
        var b = AddTeamOfTeams("Beta", "BB");
        Link(a, b);
        Link(b, a);

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert — both are on the chart; one is the root of the other, whichever the walk met first
        result.Total.Should().Be(2);
        var root = result.Organization.Should().ContainSingle().Subject;
        var child = root.Children.Should().ContainSingle().Subject;
        new[] { root.Code, child.Code }.Should().BeEquivalentTo("AA", "BB");
        child.Children.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UsesTodayWhenNoDateIsGiven()
    {
        // Arrange
        AddTeam("Cards", "TEAM");

        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.AsOfDate.Should().Be(AsOf);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsAnEmptyChart_WhenThereAreNoTeams()
    {
        // Act
        var result = await _handler.Handle(new GetFunctionalOrganizationChartQuery(AsOf), TestContext.Current.CancellationToken);

        // Assert
        result.Total.Should().Be(0);
        result.MaxDepth.Should().Be(0);
        result.Organization.Should().BeEmpty();
    }

    private Team AddTeam(string name, string code, LocalDate? activeDate = null)
    {
        var team = Team.Create(name, new TeamCode(code), null, activeDate ?? LastYear,
            Methodology.Kanban, SizingMethod.Count, EventActor.System, Now);
        _dbContext.AddTeam(team);
        return team;
    }

    private TeamOfTeams AddTeamOfTeams(string name, string code)
    {
        var teamOfTeams = TeamOfTeams.Create(name, new TeamCode(code), null, LastYear, EventActor.System, Now);
        _dbContext.AddTeamOfTeams(teamOfTeams);
        return teamOfTeams;
    }

    private static void Place(BaseTeam child, TeamOfTeams parent, LocalDate? start = null, LocalDate? end = null)
    {
        var placed = child.AddTeamMembership(parent, new MembershipDateRange(start ?? LastYear, end), Now);
        placed.IsSuccess.Should().BeTrue(placed.IsFailure ? placed.Error : null);
    }

    private static readonly FieldInfo ParentMembershipsField =
        typeof(BaseTeam).GetField("_parentMemberships", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Attaches a membership the domain would refuse (a cycle, an inactive parent, a deleted edge), by
    /// writing the backing list directly.
    /// </summary>
    private static void Link(BaseTeam child, TeamOfTeams parent, bool isDeleted = false)
    {
        var membership = new TeamMembershipFaker().WithSourceId(child.Id).WithTargetId(parent.Id).WithStartDate(LastYear).Generate();
        membership.IsDeleted = isDeleted;
        ((List<TeamMembership>)ParentMembershipsField.GetValue(child)!).Add(membership);
    }
}
