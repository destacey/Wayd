using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Domain.Tests.Sut.Models.Planning.Iterations;

public sealed class IterationDateRangeTests
{
    [Fact]
    public void Constructor_ShouldSetStartAndEnd_WhenValidDatesProvided()
    {
        // Arrange
        var start = new LocalDate(2025, 1, 1);
        var end = new LocalDate(2025, 12, 31);

        // Act
        var range = new IterationDateRange(start, end);

        // Assert
        range.Start.Should().Be(start);
        range.End.Should().Be(end);
    }

    [Fact]
    public void Constructor_ShouldAllowNullStartAndEnd()
    {
        // Act
        var range = new IterationDateRange(null, null);

        // Assert
        range.Start.Should().BeNull();
        range.End.Should().BeNull();
        range.EffectiveStart.Should().Be(LocalDate.MinIsoValue);
        range.EffectiveEnd.Should().Be(LocalDate.MaxIsoValue);
    }

    [Fact]
    public void Constructor_ShouldAllowNullStart_WhenEndProvided()
    {
        // Arrange
        var end = new LocalDate(2025, 12, 31);

        // Act
        var range = new IterationDateRange(null, end);

        // Assert
        range.Start.Should().BeNull();
        range.End.Should().Be(end);
        range.EffectiveStart.Should().Be(LocalDate.MinIsoValue);
    }

    [Fact]
    public void Constructor_ShouldAllowOneDayRange()
    {
        // Arrange
        var day = new LocalDate(2025, 1, 1);

        // Act
        var range = new IterationDateRange(day, day);

        // Assert
        range.Days.Should().Be(1);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenBothStartAndEndProvidedAndEndIsBeforeStart()
    {
        // Arrange
        var start = new LocalDate(2025, 1, 1);
        var end = new LocalDate(2024, 12, 31);

        // Act
        Action act = () => new IterationDateRange(start, end);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("The start date must be on or before the end date.*");
    }

    [Fact]
    public void Includes_ShouldReturnTrue_WhenValueIsWithinRange()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));

        // Act
        var result = range.Includes(new LocalDate(2025, 6, 15));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Includes_ShouldReturnTrue_OnTheFirstAndLastDay()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 6), new LocalDate(2025, 1, 17));

        // Act
        var first = range.Includes(new LocalDate(2025, 1, 6));
        var last = range.Includes(new LocalDate(2025, 1, 17));

        // Assert
        first.Should().BeTrue();
        last.Should().BeTrue();
    }

    [Fact]
    public void Includes_ShouldReturnFalse_WhenValueIsOutsideRange()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));

        // Act
        var before = range.Includes(new LocalDate(2024, 12, 31));
        var after = range.Includes(new LocalDate(2026, 1, 1));

        // Assert
        before.Should().BeFalse();
        after.Should().BeFalse();
    }

    [Fact]
    public void Includes_ShouldTreatNullAsMinIsoValue_WhenValueIsNull()
    {
        // Arrange
        var rangeWithStart = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));
        var rangeWithNullStart = new IterationDateRange(null, new LocalDate(2025, 12, 31));

        // Act
        var resultWhenStartPresent = rangeWithStart.Includes((LocalDate?)null);
        var resultWhenStartNull = rangeWithNullStart.Includes((LocalDate?)null);

        // Assert
        resultWhenStartPresent.Should().BeFalse();
        resultWhenStartNull.Should().BeTrue();
    }

    [Fact]
    public void Overlaps_ShouldReturnTrue_WhenRangesOverlap()
    {
        // Arrange
        var range1 = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));
        var range2 = new IterationDateRange(new LocalDate(2025, 6, 1), new LocalDate(2026, 6, 1));

        // Act
        var result = range1.Overlaps(range2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Overlaps_ShouldReturnTrue_WhenRangesShareOnlyADay()
    {
        // Arrange
        var range1 = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 1, 14));
        var range2 = new IterationDateRange(new LocalDate(2025, 1, 14), new LocalDate(2025, 1, 28));

        // Act
        var result = range1.Overlaps(range2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Overlaps_ShouldReturnFalse_WhenRangesAreConsecutive()
    {
        // Arrange
        var range1 = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 1, 14));
        var range2 = new IterationDateRange(new LocalDate(2025, 1, 15), new LocalDate(2025, 1, 28));

        // Act
        var result = range1.Overlaps(range2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Days_ShouldCountBothTheFirstAndLastDay()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 1, 31));

        // Act
        var days = range.Days;

        // Assert
        days.Should().Be(31);
    }

    [Fact]
    public void Days_ShouldReturnMaxDays_WhenEndIsNull()
    {
        // Arrange
        var start = new LocalDate(2025, 1, 1);
        var range = new IterationDateRange(start, null);

        // Act
        var days = range.Days;

        // Assert
        days.Should().Be(Period.DaysBetween(start, LocalDate.MaxIsoValue) + 1);
    }

    [Fact]
    public void IsPastOn_ShouldReturnTrue_WhenDateIsAfterTheLastDay()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 6, 30));

        // Act
        var result = range.IsPastOn(new LocalDate(2025, 7, 1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPastOn_ShouldReturnFalse_OnTheLastDay()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 6, 30));

        // Act
        var result = range.IsPastOn(new LocalDate(2025, 6, 30));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActiveOn_ShouldReturnTrue_WhenDateIsWithinRange()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));

        // Act
        var result = range.IsActiveOn(new LocalDate(2025, 6, 15));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFutureOn_ShouldReturnTrue_WhenDateIsBeforeStart()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), null);

        // Act
        var result = range.IsFutureOn(new LocalDate(2024, 12, 31));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFutureOn_ShouldReturnFalse_OnTheFirstDay()
    {
        // Arrange
        var range = new IterationDateRange(new LocalDate(2025, 1, 1), null);

        // Act
        var result = range.IsFutureOn(new LocalDate(2025, 1, 1));

        // Assert
        result.Should().BeFalse();
    }
}
