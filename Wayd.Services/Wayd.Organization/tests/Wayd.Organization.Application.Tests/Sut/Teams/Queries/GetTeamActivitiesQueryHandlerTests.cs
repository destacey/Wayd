using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetTeamActivitiesQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();

    public GetTeamActivitiesQueryHandlerTests()
    {
        _handler = new GetTeamActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTeamDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetTeamActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsActivities_WhenTeamExists()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        var expectedActivities = new List<ActivityLogDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "TeamCreatedEvent",
                DomainArea = "Organization",
                AggregateType = "Team",
                AggregateId = team.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Team Created"
            }
        };

        var expectedResponse = new PagedResponse<ActivityLogDto>(expectedActivities, 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(team.Id, "Team", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(
            new GetTeamActivitiesQuery(new IdOrKey(team.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);
    }

    public void Dispose() => _dbContext.Dispose();
}
