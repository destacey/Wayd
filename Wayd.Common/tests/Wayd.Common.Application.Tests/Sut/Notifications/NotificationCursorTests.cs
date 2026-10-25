using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications;

public sealed class NotificationCursorTests
{
    private const string OwnerType = "planning.planning-interval-objective";
    private const string OwnerId = "0192f3a1b2c34d5e8f6a7b8c9d0e1f2a";

    [Fact]
    public void TryDecode_ShouldReturnThePositionThatWasEncoded_ToTheTick()
    {
        // Arrange
        var original = new NotificationCursor(
            Instant.FromUtc(2026, 10, 25, 9, 30).PlusTicks(1234567), OwnerType, Guid.CreateVersion7());

        // Act
        var decoded = NotificationCursor.TryDecode(original.Encode(), out var cursor);

        // Assert
        decoded.Should().BeTrue();
        cursor.Should().Be(original);
    }

    [Fact]
    public void TryDecode_ShouldKeepAnInstantBeforeTheUnixEpoch()
    {
        // Arrange
        var original = new NotificationCursor(Instant.FromUtc(1969, 7, 20, 20, 17), OwnerType, Guid.CreateVersion7());

        // Act
        NotificationCursor.TryDecode(original.Encode(), out var cursor);

        // Assert
        cursor.Should().Be(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void TryDecode_ShouldRefuseAnythingThatIsNotACursor(string? value)
    {
        // Act
        var decoded = NotificationCursor.TryDecode(value, out var cursor);

        // Assert
        decoded.Should().BeFalse();
        cursor.Should().Be(default(NotificationCursor));
    }

    [Theory]
    [InlineData("abc", OwnerType, OwnerId)]
    [InlineData("99999999999999999999", OwnerType, OwnerId)]
    [InlineData("638000000000000000", " ", OwnerId)]
    [InlineData("638000000000000000", OwnerType, "not-a-guid")]
    [InlineData("638000000000000000", OwnerType, "0192f3a1-b2c3-4d5e-8f6a-7b8c9d0e1f2a")]
    public void TryDecode_ShouldRefuseACursorWhosePartsAreNotAPosition(string ticks, string ownerType, string ownerId)
    {
        // Arrange
        var value = CursorEncoding.Encode(ticks, ownerType, ownerId);

        // Act & Assert
        NotificationCursor.TryDecode(value, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldRefuseAnOwnerTypeLongerThanAnyRegisteredOne()
    {
        // Arrange
        var value = CursorEncoding.Encode("638000000000000000", new string('a', 65), OwnerId);

        // Act & Assert
        NotificationCursor.TryDecode(value, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldRefuseAnInstantBeforeTheYearOne()
    {
        // Arrange — NodaTime holds it, but the database is given instants through DateTime, which cannot.
        var beforeYearOne = Instant.FromUtc(1, 1, 1, 0, 0).Minus(Duration.FromDays(1)).ToUnixTimeTicks();
        var value = CursorEncoding.Encode(
            beforeYearOne.ToString(System.Globalization.CultureInfo.InvariantCulture), OwnerType, OwnerId);

        // Act & Assert
        NotificationCursor.TryDecode(value, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldAcceptTheFirstInstantTheDatabaseCanCompare()
    {
        // Arrange
        var original = new NotificationCursor(Instant.FromUtc(1, 1, 1, 0, 0), OwnerType, Guid.CreateVersion7());

        // Act & Assert
        NotificationCursor.TryDecode(original.Encode(), out var cursor).Should().BeTrue();
        cursor.Should().Be(original);
    }
}
