using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetFollowedRecordQueryHandlerTests
{
    private const string GadgetType = "test.followed-record.gadget";

    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant FollowedOn = Instant.FromUtc(2026, 10, 10, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetFollowedRecordQueryHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        ActivityReturns([]);
    }

    private GetFollowedRecordQueryHandler CreateHandler() =>
        new(_dbContext, _dbContext, _activityLogReader.Object, [_gadgets], _currentPrincipal.Object);

    private void SeedFollow(Guid ownerId, Guid? employeeId = null) =>
        _dbContext.Follows.Add(Follow.Create(GadgetType, ownerId, employeeId ?? _employeeId, FollowedOn).Value);

    private void ActivityReturns(ActivityLogDto[] newestFirst, bool hasNextPage = false) =>
        _activityLogReader
            .Setup(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), 1, GetFollowedRecordQueryHandler.ChangeLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<ActivityLogDto>(
                [.. newestFirst],
                hasNextPage ? newestFirst.Length + 1 : newestFirst.Length,
                1,
                hasNextPage ? newestFirst.Length : Math.Max(newestFirst.Length, 1)));

    private static ActivityLogDto Activity(Instant timestamp) => new()
    {
        EventType = "TestChangedEvent",
        DomainArea = "Test",
        AggregateType = "Test",
        Payload = "{}",
        Timestamp = timestamp,
    };

    [Fact]
    public async Task Handle_ShouldReturnTheRecordWithOnlyTheActivitySinceFollowing()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket", health: HealthStatus.Healthy, lifecycle: LifecycleCategory.Active);
        SeedFollow(gadget.Id);
        var after = Activity(FollowedOn.Plus(Duration.FromDays(3)));
        var atFollow = Activity(FollowedOn);
        var before = Activity(FollowedOn.Minus(Duration.FromDays(1)));
        ActivityReturns([after, atFollow, before]);

        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var detail = result.Value!;
        detail.Record.Id.Should().Be(gadget.Id);
        detail.Record.Key.Should().Be("G-1");
        detail.Record.Health.Should().Be(HealthStatus.Healthy);
        detail.Record.FollowedOn.Should().Be(FollowedOn);
        detail.People.Should().Equal(new Wayd.Common.Application.Records.RecordPerson("Owner", "Avery Chen", null));
        detail.ChangesSinceFollowed.Should().Equal(after, atFollow);
        detail.HasMoreChanges.Should().BeFalse();
        _activityLogReader.Verify(r => r.Read(gadget.Id, FakeRecordReadRule.ActivityType, 1, GetFollowedRecordQueryHandler.ChangeLimit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSayThereIsMore_WhenEveryEntryReadIsSinceFollowingAndAnotherPageExists()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id);
        ActivityReturns([Activity(FollowedOn.Plus(Duration.FromDays(2))), Activity(FollowedOn.Plus(Duration.FromDays(1)))], hasNextPage: true);

        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.ChangesSinceFollowed.Should().HaveCount(2);
        result.Value.HasMoreChanges.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheCallerDoesNotFollowTheRecord()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id, employeeId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_AndReadNoActivity_WhenTheCallerCanNoLongerReadIt()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id);
        _gadgets.Hide(gadget.Id);

        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
        _activityLogReader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new GetFollowedRecordQuery("test.followed-record.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }
}
