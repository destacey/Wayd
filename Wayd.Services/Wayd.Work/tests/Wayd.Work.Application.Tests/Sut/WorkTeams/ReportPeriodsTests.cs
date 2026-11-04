using FluentAssertions;
using NodaTime;
using Wayd.Work.Application.WorkTeams;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams;

public sealed class ReportPeriodsTests
{
    [Fact]
    public void Between_UpToAMonth_SplitsIntoWeeks()
    {
        // Arrange
        var from = new LocalDate(2026, 7, 1);
        var to = new LocalDate(2026, 7, 31);

        // Act
        var periods = ReportPeriods.Between(from, to);

        // Assert
        periods.Should().Equal(
            (from, new LocalDate(2026, 7, 7)),
            (new LocalDate(2026, 7, 8), new LocalDate(2026, 7, 14)),
            (new LocalDate(2026, 7, 15), new LocalDate(2026, 7, 21)),
            (new LocalDate(2026, 7, 22), new LocalDate(2026, 7, 28)),
            (new LocalDate(2026, 7, 29), to));
    }

    [Fact]
    public void Between_UpToSixMonths_SplitsIntoTwoWeeks()
    {
        // Arrange: 184 days, the longest range still split in two weeks.
        var from = new LocalDate(2026, 1, 1);
        var to = from.PlusDays(183);

        // Act
        var periods = ReportPeriods.Between(from, to);

        // Assert
        periods.Should().HaveCount(14);
        periods[0].Should().Be((from, from.PlusDays(13)));
        periods[^1].Should().Be((from.PlusDays(182), to));
    }

    [Fact]
    public void Between_LongerThanSixMonths_SplitsIntoCalendarMonths()
    {
        // Arrange
        var from = new LocalDate(2026, 1, 15);
        var to = new LocalDate(2026, 8, 10);

        // Act
        var periods = ReportPeriods.Between(from, to);

        // Assert
        periods.Should().HaveCount(8);
        periods[0].Should().Be((from, new LocalDate(2026, 1, 31)));
        periods[1].Should().Be((new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)));
        periods[^1].Should().Be((new LocalDate(2026, 8, 1), to));
    }
}
