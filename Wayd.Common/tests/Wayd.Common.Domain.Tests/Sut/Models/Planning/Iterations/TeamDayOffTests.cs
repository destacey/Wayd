using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Domain.Tests.Sut.Models.Planning.Iterations;

public sealed class TeamDayOffTests
{
    private static readonly LocalDate Day = new(2026, 10, 1);

    [Theory]
    [InlineData(100, "2026-10-01")]
    [InlineData(50, "2026-10-01:50")]
    [InlineData(1, "2026-10-01:1")]
    public void ToString_GivesTheDateAloneForAFullDayAndAppendsThePercentForAPartDay(int percentOff, string expected)
    {
        // Act
        var text = new TeamDayOff(Day, percentOff).ToString();

        // Assert
        text.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-10-01", 100)]
    [InlineData("2026-10-01:100", 100)]
    [InlineData("2026-10-01:50", 50)]
    [InlineData(" 2026-10-01 : 25 ", 25)]
    public void Parse_ReadsTheDateAndPercent_WithABareDateAsAFullDay(string text, int expectedPercent)
    {
        // Act
        var day = TeamDayOff.Parse(text);

        // Assert
        day.Should().Be(new TeamDayOff(Day, expectedPercent));
    }

    [Theory]
    [InlineData("")]
    [InlineData("01/10/2026")]
    [InlineData("2026-10-01:0")]
    [InlineData("2026-10-01:101")]
    [InlineData("2026-10-01:-5")]
    [InlineData("2026-10-01:half")]
    [InlineData("2026-10-01:50:50")]
    public void TryParse_RejectsAnythingButADateAndAPercentFromOneTo100(string text)
    {
        // Act
        var parsed = TeamDayOff.TryParse(text, out var day);

        // Assert
        parsed.Should().BeFalse();
        day.Should().BeNull();
        FluentActions.Invoking(() => TeamDayOff.Parse(text)).Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData(100, true, 0)]
    [InlineData(50, false, 0.5)]
    [InlineData(25, false, 0.75)]
    public void IsFullDayAndShareWorked_FollowThePercentOff(int percentOff, bool isFullDay, decimal shareWorked)
    {
        // Act
        var day = new TeamDayOff(Day, percentOff);

        // Assert
        day.IsFullDay.Should().Be(isFullDay);
        day.ShareWorked.Should().Be(shareWorked);
    }
}
