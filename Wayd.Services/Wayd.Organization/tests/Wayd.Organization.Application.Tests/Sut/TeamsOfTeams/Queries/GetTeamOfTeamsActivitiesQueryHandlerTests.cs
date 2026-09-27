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
    private readonly TeamFaker _teamFaker = new();

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
                Id = 1,
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

    [Fact]
    public async Task Handle_NamesTheChildTeamARelatedEntryWasRaisedOn()
    {
        // Arrange
        var parent = _teamOfTeamsFaker.Generate();
        var child = _teamFaker.Generate();
        _dbContext.AddTeamOfTeams(parent);
        _dbContext.AddTeam(child);

        var own = Activity(parent.Id, isRelated: false);
        var joined = Activity(child.Id, isRelated: true);

        _activityLogReader
            .Setup(r => r.Read(parent.Id, "Team", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([joined, own], 2, 1, 50));

        // Act
        var result = await _handler.Handle(
            new GetTeamOfTeamsActivitiesQuery(new IdOrKey(parent.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        var items = result.Value!.Items;
        items.Single(i => i.Id == joined.Id).RaisedOn.Should().BeEquivalentTo(new { child.Id, child.Key, child.Name });
        items.Single(i => i.Id == own.Id).RaisedOn.Should().BeNull("an entry raised on this team of teams needs no pointer back to it");
    }

    [Fact]
    public async Task Handle_LeavesRaisedOnEmpty_WhenTheRelatedTeamNoLongerExists()
    {
        // Arrange
        var parent = _teamOfTeamsFaker.Generate();
        _dbContext.AddTeamOfTeams(parent);

        var fromRemoved = Activity(Guid.CreateVersion7(), isRelated: true);

        _activityLogReader
            .Setup(r => r.Read(parent.Id, "Team", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([fromRemoved], 1, 1, 50));

        // Act
        var result = await _handler.Handle(
            new GetTeamOfTeamsActivitiesQuery(new IdOrKey(parent.Id), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        var item = result.Value!.Items.Should().ContainSingle().Subject;
        item.IsRelated.Should().BeTrue();
        item.RaisedOn.Should().BeNull();
    }

    // The tests tell entries apart by id, so each call needs its own.
    private static long _nextActivityId;

    private static ActivityLogDto Activity(Guid aggregateId, bool isRelated) => new()
    {
        Id = Interlocked.Increment(ref _nextActivityId),
        EventType = "TeamMembershipAddedEvent",
        DomainArea = "Organization",
        AggregateType = "Team",
        AggregateId = aggregateId,
        ActorKind = EventActorKind.User,
        Timestamp = Instant.FromUnixTimeSeconds(100),
        Payload = "{}",
        Summary = "Team Membership Added",
        IsRelated = isRelated,
    };

    public void Dispose() => _dbContext.Dispose();
}
