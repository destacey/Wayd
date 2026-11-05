using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Discussions;

public sealed class DiscussionCursorTests
{
    [Fact]
    public void TryDecode_ShouldReadBackWhatEncodeWrote()
    {
        // Arrange
        var cursor = new DiscussionCursor(Instant.FromUtc(2026, 10, 29, 9, 30, 15), Guid.NewGuid());

        // Act
        var decoded = DiscussionCursor.TryDecode(cursor.Encode(), out var read);

        // Assert
        decoded.Should().BeTrue();
        read.Should().Be(cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    public void TryDecode_ShouldRefuseAnythingThatIsNotACursor(string? value)
    {
        // Arrange — the value comes from the theory's data.

        // Act
        var decoded = DiscussionCursor.TryDecode(value, out _);

        // Assert
        decoded.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShouldRefuseAnInstantTheDatabaseCannotCompare()
    {
        // Arrange — an instant past datetime2's range, which SQL Server would reject in the page's filter. Built by
        // hand, since no Instant that far out can be made to encode one.
        var tooLate = CursorEncoding.Encode(
            (long.MaxValue / 2).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Guid.NewGuid().ToString("N"));

        // Act
        var decoded = DiscussionCursor.TryDecode(tooLate, out _);

        // Assert
        decoded.Should().BeFalse();
    }
}
