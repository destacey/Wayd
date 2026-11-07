using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamTimeZoneHistoryQueryHandlerTests : IDisposable
{
    private static readonly LocalDate FirstStart = new(2024, 1, 1);
    private static readonly LocalDate MoveDate = new(2024, 7, 1);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamTimeZoneHistoryQueryHandler _handler;

    public GetTeamTimeZoneHistoryQueryHandlerTests()
    {
        _handler = new GetTeamTimeZoneHistoryQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_Team_ReturnsEveryModelsZoneInDateOrder()
    {
        // Arrange
        var team = new TeamFaker().Generate();
        team.SetOperatingModel(FirstStart, Methodology.Scrum, SizingMethod.StoryPoints, "America/New_York", 1, WorkingWeek.MondayToFriday, null, null, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        team.SetOperatingModel(MoveDate, Methodology.Scrum, SizingMethod.StoryPoints, "Asia/Tokyo", 1, WorkingWeek.MondayToFriday, null, null, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeam(team);

        // Act
        var result = await _handler.Handle(new GetTeamTimeZoneHistoryQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Where(p => p.Start >= FirstStart).Should().Equal(
            new TeamTimeZonePeriod(FirstStart, MoveDate.PlusDays(-1), "America/New_York"),
            new TeamTimeZonePeriod(MoveDate, null, "Asia/Tokyo"));
    }

    [Fact]
    public async Task Handle_TeamOfTeams_ReturnsItsOwnModelsZones()
    {
        // Arrange
        var teamOfTeams = new TeamOfTeamsFaker().Generate();
        teamOfTeams.SetOperatingModel(FirstStart, "Europe/London", EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        teamOfTeams.SetOperatingModel(MoveDate, "America/Chicago", EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeamOfTeams(teamOfTeams);

        // Act
        var result = await _handler.Handle(new GetTeamTimeZoneHistoryQuery(teamOfTeams.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Equal(
            new TeamTimeZonePeriod(FirstStart, MoveDate.PlusDays(-1), "Europe/London"),
            new TeamTimeZonePeriod(MoveDate, null, "America/Chicago"));
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNothing()
    {
        // Act
        var result = await _handler.Handle(new GetTeamTimeZoneHistoryQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
