using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamStructureQueryHandlerTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 30);
    private static readonly Instant Timestamp = Instant.FromUtc(2025, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamStructureQueryHandler _handler;

    public GetTeamStructureQueryHandlerTests()
    {
        _handler = new GetTeamStructureQueryHandler(_dbContext);
    }

    private Team NewTeam()
    {
        var team = new TeamFaker(ActiveDate).Generate();
        _dbContext.AddTeam(team);
        return team;
    }

    private TeamOfTeams NewTeamOfTeams()
    {
        var teamOfTeams = new TeamOfTeamsFaker(ActiveDate).Generate();
        _dbContext.AddTeamOfTeams(teamOfTeams);
        return teamOfTeams;
    }

    private static void Join(BaseTeam child, TeamOfTeams parent, LocalDate start, LocalDate? end = null)
    {
        child.AddTeamMembership(parent, new MembershipDateRange(start, end), Timestamp).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        var query = new GetTeamStructureQuery(Guid.NewGuid(), From, To);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TeamOfTeams_IncludesEveryLevelBeneathIt()
    {
        // Arrange
        var root = NewTeamOfTeams();
        var middle = NewTeamOfTeams();
        var leaf = NewTeam();
        var direct = NewTeam();
        var unrelated = NewTeam();
        Join(middle, root, ActiveDate);
        Join(leaf, middle, ActiveDate);
        Join(direct, root, ActiveDate);

        // Act
        var result = await _handler.Handle(new GetTeamStructureQuery(root.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.RootId.Should().Be(root.Id);
        result.Teams.Select(t => t.Id).Should().BeEquivalentTo([root.Id, middle.Id, leaf.Id, direct.Id]);
        result.Teams.Should().NotContain(t => t.Id == unrelated.Id);
        result.Teams.Single(t => t.Id == middle.Id).Type.Should().Be(TeamType.TeamOfTeams);
        result.Memberships.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_MembershipEndedBeforeTheWindow_IsLeftOut()
    {
        // Arrange
        var root = NewTeamOfTeams();
        var former = NewTeam();
        Join(former, root, ActiveDate, From.PlusDays(-1));

        // Act
        var result = await _handler.Handle(new GetTeamStructureQuery(root.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        result!.Teams.Select(t => t.Id).Should().BeEquivalentTo([root.Id]);
        result.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TeamThatMovedWithinTheRoot_IsListedOnceWithBothEdges()
    {
        // Arrange
        var root = NewTeamOfTeams();
        var first = NewTeamOfTeams();
        var second = NewTeamOfTeams();
        var mover = NewTeam();
        Join(first, root, ActiveDate);
        Join(second, root, ActiveDate);
        var moveDate = new LocalDate(2026, 8, 15);
        Join(mover, first, ActiveDate, moveDate.PlusDays(-1));
        Join(mover, second, moveDate);

        // Act
        var result = await _handler.Handle(new GetTeamStructureQuery(root.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        result!.Teams.Count(t => t.Id == mover.Id).Should().Be(1);
        result.Memberships.Where(m => m.ChildId == mover.Id).Select(m => m.ParentId)
            .Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task Handle_ReturnsSizingPeriodsThatOverlapTheWindow()
    {
        // Arrange
        var team = NewTeam();
        team.SetOperatingModel(ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1).IsSuccess.Should().BeTrue();
        team.SetOperatingModel(new LocalDate(2026, 8, 1), Methodology.Kanban, SizingMethod.Count, "UTC", 1).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamStructureQuery(team.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        result!.Teams.Select(t => t.Id).Should().BeEquivalentTo([team.Id]);
        result.SizingPeriods.Should().BeEquivalentTo(new[]
        {
            new TeamSizingPeriod(team.Id, ActiveDate, new LocalDate(2026, 7, 31), true),
            new TeamSizingPeriod(team.Id, new LocalDate(2026, 8, 1), null, false),
        });
    }

    [Fact]
    public async Task Handle_SizingPeriodEndedBeforeTheWindow_IsLeftOut()
    {
        // Arrange
        var team = NewTeam();
        team.SetOperatingModel(ActiveDate, Methodology.Scrum, SizingMethod.Count, "UTC", 1).IsSuccess.Should().BeTrue();
        team.SetOperatingModel(From, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamStructureQuery(team.Id, From, To), TestContext.Current.CancellationToken);

        // Assert
        result!.SizingPeriods.Should().ContainSingle()
            .Which.UsesStoryPoints.Should().BeTrue();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
