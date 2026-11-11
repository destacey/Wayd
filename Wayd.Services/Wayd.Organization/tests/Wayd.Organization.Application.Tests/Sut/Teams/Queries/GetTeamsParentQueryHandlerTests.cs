using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamsParentQueryHandlerTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);
    private static readonly LocalDate Today = new(2026, 11, 10);
    private static readonly Instant Timestamp = Instant.FromUtc(2025, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();

    private GetTeamsParentQueryHandler CreateHandler() => new(_dbContext);

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

    private static void Join(BaseTeam child, TeamOfTeams parent, LocalDate start, LocalDate? end = null) =>
        child.AddTeamMembership(parent, new MembershipDateRange(start, end), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task Handle_ReturnsTheTeamOfTeamsEachTeamBelongsToOnTheDate()
    {
        // Arrange
        var formerParent = NewTeamOfTeams();
        var parent = NewTeamOfTeams();
        var moved = NewTeam();
        var unparented = NewTeam();
        Join(moved, formerParent, ActiveDate, Today.PlusDays(-1));
        Join(moved, parent, Today);

        // Act
        var result = await CreateHandler().Handle(new GetTeamsParentQuery([moved.Id, unparented.Id], Today), TestContext.Current.CancellationToken);

        // Assert
        result.Keys.Should().Equal(moved.Id);
        var found = result[moved.Id];
        found.Id.Should().Be(parent.Id);
        found.Key.Should().Be(parent.Key);
        found.Name.Should().Be(parent.Name);
        found.Code.Should().Be(parent.Code.Value);
        found.Type.Should().Be("Team of Teams");
    }

    [Fact]
    public async Task Handle_MembershipNotYetBegun_IsLeftOut()
    {
        // Arrange
        var parent = NewTeamOfTeams();
        var joining = NewTeam();
        Join(joining, parent, Today.PlusDays(1));

        // Act
        var result = await CreateHandler().Handle(new GetTeamsParentQuery([joining.Id], Today), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
