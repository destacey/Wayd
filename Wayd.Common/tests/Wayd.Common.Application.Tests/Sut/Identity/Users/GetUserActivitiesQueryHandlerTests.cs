using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Identity.Users.Queries;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.Identity.Users;

public sealed class GetUserActivitiesQueryHandlerTests : IDisposable
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetUserActivitiesQueryHandler _handler;

    public GetUserActivitiesQueryHandlerTests()
    {
        _userService
            .Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsDto { Id = UserId, LoginProvider = "Wayd" });

        _handler = new GetUserActivitiesQueryHandler(_dbContext, _userService.Object, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUserDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetUserActivitiesQuery(Guid.NewGuid().ToString()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenIdIsNotAGuid()
    {
        // Act
        var result = await _handler.Handle(new GetUserActivitiesQuery("not-a-guid"), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReadsTheUsersActivity()
    {
        // Arrange
        var expected = new PagedResponse<ActivityLogDto>([Activity(Guid.Parse(UserId), "ApplicationUser", isRelated: false)], 1, 1, 50);
        _activityLogReader
            .Setup(r => r.Read(Guid.Parse(UserId), "ApplicationUser", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.Handle(new GetUserActivitiesQuery(UserId), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_NamesTheTokenARelatedEntryWasRaisedOn()
    {
        // Arrange
        var token = new PersonalAccessTokenFaker().WithUserId(UserId).WithName("Build agent").Generate();
        _dbContext.PersonalAccessTokens.Add(token);

        var fromToken = Activity(token.Id, "PersonalAccessToken", isRelated: true);
        var fromDeletedToken = Activity(Guid.NewGuid(), "PersonalAccessToken", isRelated: true);
        _activityLogReader
            .Setup(r => r.Read(Guid.Parse(UserId), "ApplicationUser", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>([fromToken, fromDeletedToken], 2, 1, 50));

        // Act
        var result = await _handler.Handle(new GetUserActivitiesQuery(UserId), TestContext.Current.CancellationToken);

        // Assert
        var items = result.Value!.Items;
        items.Single(i => i.Id == fromToken.Id).RaisedOn!.Name.Should().Be("Build agent");
        items.Single(i => i.Id == fromDeletedToken.Id).RaisedOn.Should().BeNull();
    }

    // The tests tell entries apart by id, so each call needs its own.
    private static long _nextActivityId;

    private static ActivityLogDto Activity(Guid aggregateId, string aggregateType, bool isRelated) => new()
    {
        Id = Interlocked.Increment(ref _nextActivityId),
        EventType = "PersonalAccessTokenRevokedEvent",
        DomainArea = "Identity",
        AggregateType = aggregateType,
        AggregateId = aggregateId,
        ActorKind = EventActorKind.User,
        Timestamp = Instant.FromUnixTimeSeconds(100),
        Payload = "{}",
        Summary = "Personal Access Token Revoked",
        IsRelated = isRelated,
    };

    public void Dispose() => _dbContext.Dispose();
}
