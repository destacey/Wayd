using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Work.Application.WorkTeams;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams;

public sealed class TeamReportCalendarTests
{
    private const string NewYork = "America/New_York";
    private const string Tokyo = "Asia/Tokyo";

    private static readonly LocalDate ChangeDay = new(2026, 8, 1);

    /// <summary>New York until July 31, Tokyo from August 1.</summary>
    private static TeamReportCalendar NewYorkThenTokyo() => TeamReportCalendar.From(
    [
        new TeamTimeZonePeriod(new LocalDate(2026, 1, 1), ChangeDay.PlusDays(-1), NewYork),
        new TeamTimeZonePeriod(ChangeDay, null, Tokyo),
    ]);

    [Fact]
    public void StartOf_SingleZone_IsMidnightInThatZone()
    {
        // Arrange
        var calendar = TeamReportCalendar.From([new TeamTimeZonePeriod(new LocalDate(2026, 1, 1), null, NewYork)]);

        // Act
        var start = calendar.StartOf(new LocalDate(2026, 7, 31));

        // Assert
        start.Should().Be(Instant.FromUtc(2026, 7, 31, 4, 0));
    }

    [Fact]
    public void DateOf_EveningInNewYork_IsThatDayNotTheNextUtcDay()
    {
        // Arrange
        var calendar = TeamReportCalendar.From([new TeamTimeZonePeriod(new LocalDate(2026, 1, 1), null, NewYork)]);

        // Act
        var date = calendar.DateOf(Instant.FromUtc(2026, 8, 1, 1, 0));

        // Assert
        date.Should().Be(new LocalDate(2026, 7, 31));
    }

    [Fact]
    public void ZoneOn_FollowsTheModelInEffectOnEachDay()
    {
        // Arrange
        var calendar = NewYorkThenTokyo();

        // Act
        var before = calendar.ZoneOn(ChangeDay.PlusDays(-1)).Id;
        var after = calendar.ZoneOn(ChangeDay).Id;
        var beforeFirstModel = calendar.ZoneOn(new LocalDate(2025, 6, 1)).Id;

        // Assert
        before.Should().Be(NewYork);
        after.Should().Be(Tokyo);
        beforeFirstModel.Should().Be(NewYork);
    }

    [Fact]
    public void StartOf_TheDayTheZoneChanges_IsMidnightInTheNewZone()
    {
        // Arrange
        var calendar = NewYorkThenTokyo();

        // Act
        var start = calendar.StartOf(ChangeDay);

        // Assert
        start.Should().Be(Instant.FromUtc(2026, 7, 31, 15, 0));
    }

    [Theory]
    [InlineData(2026, 7, 31, 14, 59, 2026, 7, 31)]
    [InlineData(2026, 7, 31, 15, 0, 2026, 8, 1)]
    [InlineData(2026, 8, 1, 14, 59, 2026, 8, 1)]
    [InlineData(2026, 8, 1, 15, 0, 2026, 8, 2)]
    public void DateOf_AroundAChangeOfZone_AgreesWithTheDaysBounds(
        int year, int month, int day, int hour, int minute, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var calendar = NewYorkThenTokyo();
        var instant = Instant.FromUtc(year, month, day, hour, minute);

        // Act
        var date = calendar.DateOf(instant);

        // Assert
        date.Should().Be(new LocalDate(expectedYear, expectedMonth, expectedDay));
        calendar.StartOf(date).Should().BeLessThanOrEqualTo(instant);
        calendar.StartOf(date.PlusDays(1)).Should().BeGreaterThan(instant);
    }

    [Fact]
    public void From_NoPeriods_CountsUtcDays()
    {
        // Arrange
        IEnumerable<TeamTimeZonePeriod>? periods = null;

        // Act
        var calendar = TeamReportCalendar.From(periods);

        // Assert
        calendar.ZoneOn(new LocalDate(2026, 7, 31)).Should().Be(DateTimeZone.Utc);
        calendar.StartOf(new LocalDate(2026, 7, 31)).Should().Be(Instant.FromUtc(2026, 7, 31, 0, 0));
    }

    [Fact]
    public void From_UnknownZone_CountsUtcDays()
    {
        // Arrange
        TeamTimeZonePeriod[] periods = [new(new LocalDate(2026, 1, 1), null, "Not/AZone")];

        // Act
        var calendar = TeamReportCalendar.From(periods);

        // Assert
        calendar.ZoneOn(new LocalDate(2026, 7, 31)).Should().Be(DateTimeZone.Utc);
    }
}
