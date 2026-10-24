using NodaTime;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Domain.Tests.Sut.Following;

public sealed class FollowTests
{
    private static readonly FollowOwnerDescriptor Gadget = new("test.follow.gadget", "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 23, 14, 30);

    public FollowTests()
    {
        FollowOwners.Register(Gadget);
    }

    [Fact]
    public void Create_ShouldRecordWhoFollowsWhatAndWhen()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        // Act
        var result = Follow.Create(Gadget.Key, ownerId, employeeId, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.OwnerType.Should().Be("test.follow.gadget");
        result.Value.OwnerId.Should().Be(ownerId);
        result.Value.EmployeeId.Should().Be(employeeId);
        result.Value.FollowedOn.Should().Be(Now);
    }

    [Fact]
    public void Create_ShouldStoreTheRegisteredSpellingOfTheOwnerType()
    {
        // Act
        var result = Follow.Create(" TEST.Follow.Gadget ", Guid.NewGuid(), Guid.NewGuid(), Now);

        // Assert
        // One casing on disk, so a case-sensitive comparison anywhere downstream still matches.
        result.Value.OwnerType.Should().Be("test.follow.gadget");
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Act
        var result = Follow.Create("test.follow.unregistered", Guid.NewGuid(), Guid.NewGuid(), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheOwnerIdIsEmpty()
    {
        // Act
        Action act = () => Follow.Create(Gadget.Key, Guid.Empty, Guid.NewGuid(), Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("ownerId");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheEmployeeIdIsEmpty()
    {
        // Act
        Action act = () => Follow.Create(Gadget.Key, Guid.NewGuid(), Guid.Empty, Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("employeeId");
    }
}
