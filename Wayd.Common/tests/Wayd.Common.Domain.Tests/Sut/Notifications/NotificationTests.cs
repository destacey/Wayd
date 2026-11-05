using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Domain.Tests.Sut.Notifications;

public sealed class NotificationTests
{
    private static readonly FollowOwnerDescriptor Gadget = new("test.notification.gadget", "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 9, 15);

    public NotificationTests()
    {
        FollowOwners.Register(Gadget);
    }

    [Fact]
    public void Create_ShouldRecordTheFactsUnread()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // Act
        var result = Notification.Create(
            recipientId, Gadget.Key, ownerId, eventId, NotificationEvent.StatusChanged,
            EventActorKind.User, actorId, Now, NotificationReason.Following | NotificationReason.Participant);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var notification = result.Value;
        notification.RecipientEmployeeId.Should().Be(recipientId);
        notification.OwnerType.Should().Be(Gadget.Key);
        notification.OwnerId.Should().Be(ownerId);
        notification.EventId.Should().Be(eventId);
        notification.Event.Should().Be(NotificationEvent.StatusChanged);
        notification.ActorKind.Should().Be(EventActorKind.User);
        notification.ActorEmployeeId.Should().Be(actorId);
        notification.OccurredOn.Should().Be(Now);
        notification.Reasons.Should().Be(NotificationReason.Following | NotificationReason.Participant);
        notification.ReadOn.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldStoreTheRegisteredSpellingOfTheOwnerType()
    {
        // Act
        var result = Create(ownerType: " TEST.Notification.Gadget ");

        // Assert
        result.Value.OwnerType.Should().Be("test.notification.gadget");
    }

    [Fact]
    public void Create_ShouldAllowAnActorWithNoEmployee()
    {
        // Act
        var result = Create(actorEmployeeId: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActorEmployeeId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Act
        var result = Create(ownerType: "test.notification.unregistered");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(1 | 256)]
    public void Create_ShouldFail_WithoutAValidReason(int reasons)
    {
        // Act
        var result = Create(reasons: (NotificationReason)reasons);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A notification needs at least one defined reason.");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheRecipientIsEmpty()
    {
        // Act
        Action act = () => Create(recipientId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheEventIdIsEmpty()
    {
        // Act
        Action act = () => Create(eventId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_ForAnUndefinedEvent()
    {
        // Act
        Action act = () => Create(@event: (NotificationEvent)999);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static CSharpFunctionalExtensions.Result<Notification> Create(
        Guid? recipientId = null,
        string? ownerType = null,
        Guid? eventId = null,
        NotificationEvent @event = NotificationEvent.TimelineChanged,
        Guid? actorEmployeeId = default,
        NotificationReason reasons = NotificationReason.Following) =>
        Notification.Create(
            recipientId ?? Guid.NewGuid(),
            ownerType ?? Gadget.Key,
            Guid.NewGuid(),
            eventId ?? Guid.NewGuid(),
            @event,
            EventActorKind.User,
            actorEmployeeId,
            Now,
            reasons);
}
