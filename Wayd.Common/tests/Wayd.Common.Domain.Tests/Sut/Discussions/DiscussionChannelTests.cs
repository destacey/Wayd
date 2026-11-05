using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Tests.Sut.Discussions;

public sealed class DiscussionChannelTests
{
    private static readonly DiscussionChannelTypeDescriptor Gadget = new("test.discussions.channel", "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 14, 30);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    public DiscussionChannelTests()
    {
        DiscussionChannelTypes.Register(Gadget);
    }

    [Fact]
    public void OpenForRecord_ShouldOpenAnUnlockedChannelUnderTheRegisteredKey()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        // Act
        var result = DiscussionChannel.OpenForRecord(" TEST.Discussions.Channel ", ownerId, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var channel = result.Value;
        channel.ChannelType.Should().Be("test.discussions.channel");
        channel.OwnerId.Should().Be(ownerId);
        channel.CreatedOn.Should().Be(Now);
        channel.IsLocked.Should().BeFalse();

        var opened = channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionChannelOpenedEvent>().Subject;
        opened.Id.Should().Be(channel.Id);
        opened.ChannelType.Should().Be("test.discussions.channel");
        opened.OwnerId.Should().Be(ownerId);
        opened.Actor.Should().Be(Actor);
        opened.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void OpenForRecord_ShouldFail_ForAnUnregisteredType()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        // Act
        var result = DiscussionChannel.OpenForRecord("test.discussions.nothing", ownerId, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.UnknownChannelType("test.discussions.nothing"));
    }

    [Fact]
    public void OpenForRecord_ShouldThrow_WhenTheOwnerIdIsEmpty()
    {
        // Arrange
        var ownerId = Guid.Empty;

        // Act
        Action act = () => DiscussionChannel.OpenForRecord(Gadget.Key, ownerId, Actor, Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    [Fact]
    public void Lock_ShouldRecordWhoLockedItAndWhen()
    {
        // Arrange
        var channel = OpenChannel();
        var moderatorId = Guid.NewGuid();
        var later = Now.Plus(Duration.FromHours(1));

        // Act
        var result = channel.Lock(moderatorId, Actor, later);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        channel.IsLocked.Should().BeTrue();
        channel.LockedOn.Should().Be(later);
        channel.LockedById.Should().Be(moderatorId);

        var locked = channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionChannelLockedEvent>().Subject;
        locked.Id.Should().Be(channel.Id);
        locked.OwnerId.Should().Be(channel.OwnerId);
        locked.LockedById.Should().Be(moderatorId);
        locked.Timestamp.Should().Be(later);
    }

    [Fact]
    public void Lock_ShouldFail_WhenAlreadyLocked()
    {
        // Arrange
        var channel = OpenChannel();
        var firstModeratorId = Guid.NewGuid();
        channel.Lock(firstModeratorId, Actor, Now);
        channel.ClearDomainEvents();

        // Act
        var result = channel.Lock(Guid.NewGuid(), Actor, Now.Plus(Duration.FromHours(1)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.AlreadyLocked);
        channel.LockedById.Should().Be(firstModeratorId);
        channel.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Unlock_ShouldClearTheLock()
    {
        // Arrange
        var channel = OpenChannel();
        channel.Lock(Guid.NewGuid(), Actor, Now);
        channel.ClearDomainEvents();

        // Act
        var result = channel.Unlock(Actor, Now.Plus(Duration.FromHours(1)));

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        channel.IsLocked.Should().BeFalse();
        channel.LockedOn.Should().BeNull();
        channel.LockedById.Should().BeNull();
        channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionChannelUnlockedEvent>()
            .Which.Id.Should().Be(channel.Id);
    }

    [Fact]
    public void Unlock_ShouldFail_WhenNotLocked()
    {
        // Arrange
        var channel = OpenChannel();

        // Act
        var result = channel.Unlock(Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.NotLocked);
        channel.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_ShouldRecordTheConversationsRemoval()
    {
        // Arrange
        var channel = OpenChannel();
        var later = Now.Plus(Duration.FromDays(3));

        // Act
        channel.Delete(Actor, later);

        // Assert
        var deleted = channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionChannelDeletedEvent>().Subject;
        deleted.Id.Should().Be(channel.Id);
        deleted.ChannelType.Should().Be(Gadget.Key);
        deleted.OwnerId.Should().Be(channel.OwnerId);
        deleted.Timestamp.Should().Be(later);
    }

    private static DiscussionChannel OpenChannel()
    {
        var channel = DiscussionChannel.OpenForRecord(Gadget.Key, Guid.NewGuid(), Actor, Now).Value;
        channel.ClearDomainEvents();
        return channel;
    }
}
