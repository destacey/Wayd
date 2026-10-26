using NodaTime;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Domain.Tests.Sut.Notifications;

public sealed class RecordMuteTests
{
    private static readonly FollowOwnerDescriptor Gadget = new("test.record-mute.gadget", "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 15);

    public RecordMuteTests()
    {
        FollowOwners.Register(Gadget);
    }

    [Fact]
    public void Create_ShouldRecordWhoMutedWhatAndWhen()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        // Act
        var result = RecordMute.Create(Gadget.Key, ownerId, employeeId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.OwnerType.Should().Be("test.record-mute.gadget");
        result.Value.OwnerId.Should().Be(ownerId);
        result.Value.EmployeeId.Should().Be(employeeId);
        result.Value.MutedOn.Should().Be(Now);
    }

    [Fact]
    public void Create_ShouldStoreTheRegisteredSpellingOfTheOwnerType()
    {
        // Act
        var result = RecordMute.Create(" TEST.Record-Mute.Gadget ", Guid.NewGuid(), Guid.NewGuid(), Now);

        // Assert
        result.Value.OwnerType.Should().Be("test.record-mute.gadget");
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Act
        var result = RecordMute.Create("test.record-mute.unregistered", Guid.NewGuid(), Guid.NewGuid(), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheOwnerIdIsEmpty()
    {
        // Act
        Action act = () => RecordMute.Create(Gadget.Key, Guid.Empty, Guid.NewGuid(), Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheEmployeeIdIsEmpty()
    {
        // Act
        Action act = () => RecordMute.Create(Gadget.Key, Guid.NewGuid(), Guid.Empty, Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("employeeId");
    }
}
