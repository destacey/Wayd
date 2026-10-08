using Wayd.Common.Domain.Models.Organizations;
using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamsScheduleHistoryQueryHandlerTests : IDisposable
{
    private static readonly LocalDate FirstStart = new(2024, 1, 1);
    private static readonly LocalDate MoveDate = new(2024, 7, 1);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamsScheduleHistoryQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();

    public GetTeamsScheduleHistoryQueryHandlerTests()
    {
        _handler = new GetTeamsScheduleHistoryQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsEachRequestedTeamsSchedulesInDateOrder_AndNoOtherTeams()
    {
        // Arrange
        var moved = _teamFaker.Generate();
        moved.SetOperatingModel(FirstStart, Methodology.Scrum, SizingMethod.StoryPoints, "America/New_York", 1, WorkingWeek.MondayToFriday, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        moved.SetOperatingModel(MoveDate, Methodology.Scrum, SizingMethod.StoryPoints, "America/Chicago", 2, WorkingWeek.MondayToFriday, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        var other = _teamFaker.Generate();
        other.SetOperatingModel(FirstStart, Methodology.Kanban, SizingMethod.Count, "Europe/London", 1, WorkingWeek.MondayToFriday, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        var notRequested = _teamFaker.Generate();
        _dbContext.AddTeam(moved);
        _dbContext.AddTeam(other);
        _dbContext.AddTeam(notRequested);

        // Act
        var result = await _handler.Handle(new GetTeamsScheduleHistoryQuery([moved.Id, other.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Keys.Should().BeEquivalentTo([moved.Id, other.Id]);
        var movedPeriods = result[moved.Id].Where(p => p.Start >= FirstStart).ToList();
        movedPeriods.Select(p => p.TimeZone).Should().Equal("America/New_York", "America/Chicago");
        result[other.Id].Should().Contain(p => p.TimeZone == "Europe/London");
    }

    [Fact]
    public async Task Handle_WhenNoTeamsAreRequested_ReturnsNothing()
    {
        // Act
        var result = await _handler.Handle(new GetTeamsScheduleHistoryQuery([]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenATeamHasNoOperatingModels_LeavesItOut()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Act
        var result = await _handler.Handle(new GetTeamsScheduleHistoryQuery([team.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotContainKey(team.Id);
    }

    [Fact]
    public async Task Handle_WhenATeamDoesNotExist_LeavesItOut()
    {
        // Act
        var result = await _handler.Handle(new GetTeamsScheduleHistoryQuery([Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
