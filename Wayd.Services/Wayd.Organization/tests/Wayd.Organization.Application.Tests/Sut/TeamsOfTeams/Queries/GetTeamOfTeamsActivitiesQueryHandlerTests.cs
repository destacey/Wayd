using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Queries;

public class GetTeamOfTeamsActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetTeamOfTeamsActivitiesQueryHandler _handler;
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();

    public GetTeamOfTeamsActivitiesQueryHandlerTests()
    {
        _handler = new GetTeamOfTeamsActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTeamOfTeamsDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetTeamOfTeamsActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheTeamAggregateType_WhenTeamOfTeamsExists()
    {
        // Arrange
        var teamOfTeams = _teamOfTeamsFaker.Generate();
        _dbContext.AddTeamOfTeams(teamOfTeams);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "TeamCreatedEvent",
                DomainArea = "Organization",
                AggregateType = "Team",
                AggregateId = teamOfTeams.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Team Created"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(teamOfTeams.Id, "Team", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetTeamOfTeamsActivitiesQuery(new IdOrKey(teamOfTeams.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
