using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Domain.Tests.Sut.Models.Organizations;

public sealed class ReportingCadenceTests
{
    private static readonly LocalDate Monday = new(2026, 10, 5);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ReportingCadence.MaxWeeks + 1)]
    public void Create_WithALengthOutOfRange_Fails(int weeks)
    {
        // Act
        var result = ReportingCadence.Create(weeks, Monday);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithALengthInRange_Succeeds()
    {
        // Act
        var result = ReportingCadence.Create(2, Monday);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Weeks.Should().Be(2);
        result.Value.Anchor.Should().Be(Monday);
        result.Value.Days.Should().Be(14);
    }

    [Theory]
    [InlineData(2026, 10, 5, 2026, 10, 5)]
    [InlineData(2026, 10, 18, 2026, 10, 5)]
    [InlineData(2026, 10, 19, 2026, 10, 19)]
    [InlineData(2026, 10, 4, 2026, 9, 21)]
    [InlineData(2026, 9, 21, 2026, 9, 21)]
    public void PeriodContaining_ReturnsTheTwoWeekPeriodAlignedToTheAnchor(int year, int month, int day, int startYear, int startMonth, int startDay)
    {
        // Arrange
        var sut = ReportingCadence.Create(2, Monday).Value;

        // Act
        var period = sut.PeriodContaining(new LocalDate(year, month, day));

        // Assert
        period.Start.Should().Be(new LocalDate(startYear, startMonth, startDay));
        period.End.Should().Be(period.Start.PlusDays(13));
    }

    [Fact]
    public void PeriodsOverlapping_ReturnsWholePeriodsCoveringTheRange()
    {
        // Arrange
        var sut = ReportingCadence.Create(2, Monday).Value;

        // Act
        var periods = sut.PeriodsOverlapping(new LocalDate(2026, 10, 1), new LocalDate(2026, 10, 20)).ToList();

        // Assert
        periods.Select(p => p.Start).Should().Equal(new LocalDate(2026, 9, 21), new LocalDate(2026, 10, 5), new LocalDate(2026, 10, 19));
        periods.Should().AllSatisfy(p => p.Start.DayOfWeek.Should().Be(IsoDayOfWeek.Monday));
    }

    [Fact]
    public void PeriodsOverlapping_WithTheEndBeforeTheStart_ReturnsNothing()
    {
        // Arrange
        var sut = ReportingCadence.Create(1, Monday).Value;

        // Act
        var periods = sut.PeriodsOverlapping(Monday, Monday.PlusDays(-1));

        // Assert
        periods.Should().BeEmpty();
    }

    [Fact]
    public void Equals_ComparesLengthAndAnchor()
    {
        // Arrange
        var a = ReportingCadence.Create(2, Monday).Value;
        var b = ReportingCadence.Create(2, Monday).Value;
        var c = ReportingCadence.Create(2, Monday.PlusDays(7)).Value;

        // Act
        var sameAnchor = a == b;
        var otherAnchor = a == c;

        // Assert
        sameAnchor.Should().BeTrue();
        otherAnchor.Should().BeFalse();
    }
}
