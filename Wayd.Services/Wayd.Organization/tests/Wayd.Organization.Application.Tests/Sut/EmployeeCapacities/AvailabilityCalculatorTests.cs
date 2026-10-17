using NodaTime;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class AvailabilityCalculatorTests
{
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid TeamCalendar = Guid.NewGuid();
    private static readonly Guid AssignedCalendar = Guid.NewGuid();
    private static readonly Guid DefaultCalendar = Guid.NewGuid();

    private static readonly LocalDate Day = new(2027, 2, 3);
    private static readonly TeamDay OnTheTeamsCalendar = new(Day, TeamCalendar);
    private static readonly TeamDay OnNoCalendar = new(Day, null);

    [Fact]
    public void Available_WithNoHolidayAndNoTimeAway_IsTheWholeDay()
    {
        // Arrange
        var sut = AvailabilityCalculator.AlwaysAvailable;

        // Act
        var available = sut.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(1m);
    }

    [Fact]
    public void Available_OnAHolidayOnlyInTheTeamsCalendar_IsTheWholeDayForSomeoneWhoFollowsNoCalendar()
    {
        // Arrange — a person's holidays never come from the team, which only reads as a whole
        var sut = Calculator(holidays: new() { [TeamCalendar] = [Day] });

        // Act
        var available = sut.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(1m);
        sut.IsTeamHoliday(OnTheTeamsCalendar).Should().BeTrue();
    }

    [Fact]
    public void Available_ForSomeoneWhoFollowsNoCalendar_TakesTheSystemDefaultWhateverTheTeamsCalendar()
    {
        // Arrange
        var sut = Calculator(holidays: new() { [DefaultCalendar] = [Day] }, defaultCalendarId: DefaultCalendar);

        // Act
        var onATeamWithACalendar = sut.Available(Employee, OnTheTeamsCalendar);
        var onATeamWithNone = sut.Available(Employee, OnNoCalendar);

        // Assert
        onATeamWithACalendar.Should().Be(0m);
        onATeamWithNone.Should().Be(0m);
        sut.Available(Employee, new TeamDay(Day.PlusDays(1), TeamCalendar)).Should().Be(1m);
    }

    [Fact]
    public void Available_ForSomeoneWhoFollowsACalendar_TakesItsHolidaysInsteadOfTheSystemDefaults()
    {
        // Arrange — the default is off on the day, the calendar they follow on the next
        var sut = Calculator(
            calendars: new() { [Employee] = AssignedCalendar },
            holidays: new() { [DefaultCalendar] = [Day], [AssignedCalendar] = [Day.PlusDays(1)] },
            defaultCalendarId: DefaultCalendar);

        // Act
        var onTheDefaultsHoliday = sut.Available(Employee, OnNoCalendar);
        var onTheirCalendarsHoliday = sut.Available(Employee, new TeamDay(Day.PlusDays(1), null));

        // Assert
        onTheDefaultsHoliday.Should().Be(1m);
        onTheirCalendarsHoliday.Should().Be(0m);
    }

    [Fact]
    public void Available_ForSomeoneWhoFollowsACalendar_IsTheSameOnEveryTeam()
    {
        // Arrange — two teams on different calendars, both off on the day
        var otherTeamCalendar = Guid.NewGuid();
        var sut = Calculator(
            calendars: new() { [Employee] = AssignedCalendar },
            holidays: new() { [TeamCalendar] = [Day], [otherTeamCalendar] = [Day] });

        // Act
        var onOneTeam = sut.Available(Employee, OnTheTeamsCalendar);
        var onTheOther = sut.Available(Employee, new TeamDay(Day, otherTeamCalendar));

        // Assert
        onOneTeam.Should().Be(1m);
        onTheOther.Should().Be(1m);
    }

    [Fact]
    public void IsTeamHoliday_ForATeamWithNoCalendar_ReadsTheSystemDefault()
    {
        // Arrange
        var sut = Calculator(holidays: new() { [DefaultCalendar] = [Day] }, defaultCalendarId: DefaultCalendar);

        // Act
        var withNone = sut.IsTeamHoliday(OnNoCalendar);
        var withItsOwn = sut.IsTeamHoliday(OnTheTeamsCalendar);

        // Assert
        withNone.Should().BeTrue();
        withItsOwn.Should().BeFalse();
    }

    [Fact]
    public void Available_ForSomeoneWhoFollowsNoCalendarWithNoSystemDefault_HasNoHolidays()
    {
        // Arrange
        var sut = Calculator(holidays: new() { [DefaultCalendar] = [Day], [TeamCalendar] = [Day] });

        // Act
        var available = sut.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(1m);
    }

    [Fact]
    public void Available_OnAHolidayOnlyInTheCalendarTheyFollow_IsNothing()
    {
        // Arrange
        var sut = Calculator(
            calendars: new() { [Employee] = AssignedCalendar },
            holidays: new() { [AssignedCalendar] = [Day] });

        // Act
        var available = sut.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(0m);
        sut.IsTeamHoliday(OnTheTeamsCalendar).Should().BeFalse();
    }

    [Fact]
    public void Available_WithTimeAway_IsWhatIsLeftOfTheDay()
    {
        // Arrange
        var sut = Calculator(timeAway: [new TimeAwaySpan(Employee, Day, Day, 25)]);

        // Act
        var available = sut.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(0.75m);
    }

    [Fact]
    public void ShareAway_CoversTheFirstAndLastDayAndNoneOutside()
    {
        // Arrange
        var sut = Calculator(timeAway: [new TimeAwaySpan(Employee, Day, Day.PlusDays(2), 100)]);

        // Act
        decimal[] shares = [.. Enumerable.Range(-1, 5).Select(offset => sut.ShareAway(Employee, Day.PlusDays(offset)))];

        // Assert
        shares.Should().Equal(0m, 1m, 1m, 1m, 0m);
    }

    [Fact]
    public void ShareAway_AddsUpOverlappingStretchesToNoMoreThanTheWholeDay()
    {
        // Arrange
        var sut = Calculator(timeAway:
        [
            new TimeAwaySpan(Employee, Day, Day.PlusDays(1), 50),
            new TimeAwaySpan(Employee, Day, Day, 30),
            new TimeAwaySpan(Employee, Day.PlusDays(1), Day.PlusDays(1), 80),
        ]);

        // Act
        var first = sut.ShareAway(Employee, Day);
        var second = sut.ShareAway(Employee, Day.PlusDays(1));

        // Assert
        first.Should().Be(0.8m);
        second.Should().Be(1m);
    }

    [Fact]
    public void ShareAway_IsOnlyThePersonsOwn()
    {
        // Arrange
        var sut = Calculator(timeAway: [new TimeAwaySpan(Guid.NewGuid(), Day, Day, 100)]);

        // Act
        var share = sut.ShareAway(Employee, Day);

        // Assert
        share.Should().Be(0m);
    }

    [Fact]
    public void TimeAwayOf_ReturnsThePersonsStretchesInDateOrder()
    {
        // Arrange
        var later = new TimeAwaySpan(Employee, Day.PlusDays(5), Day.PlusDays(6), 100);
        var earlier = new TimeAwaySpan(Employee, Day, Day, 50);
        var sut = Calculator(timeAway: [later, new TimeAwaySpan(Guid.NewGuid(), Day, Day, 100), earlier]);

        // Act
        var timeAway = sut.TimeAwayOf(Employee);

        // Assert
        timeAway.Should().Equal(earlier, later);
    }

    [Fact]
    public void PersonDaysAndOwnWorkingDayHours_ForSomeoneWithTheirOwnWorkingDay_ScaleByIt()
    {
        // Arrange
        var calculator = Calculator(lengths: new WorkingDayLengths(7.5m, new Dictionary<Guid, decimal> { [Employee] = 3m }));

        // Act
        var personDays = calculator.PersonDays(Employee, 0.5m);
        var hours = calculator.OwnWorkingDayHours(Employee);

        // Assert
        personDays.Should().Be(0.2m);
        hours.Should().Be(3m);
    }

    [Fact]
    public void PersonDaysAndOwnWorkingDayHours_ForSomeoneWithoutTheirOwn_AreUnscaledAndNone()
    {
        // Arrange
        var calculator = Calculator(lengths: new WorkingDayLengths(7.5m, new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 3m }));

        // Act
        var personDays = calculator.PersonDays(Employee, 0.5m);
        var hours = calculator.OwnWorkingDayHours(Employee);

        // Assert
        personDays.Should().Be(0.5m);
        hours.Should().BeNull();
    }

    [Fact]
    public void PersonDays_ForAShareThatNeverTerminates_KeepsTenDecimalPlaces()
    {
        // Arrange — 4 hours of a 7.5-hour day is 0.5333…
        var calculator = Calculator(lengths: new WorkingDayLengths(7.5m, new Dictionary<Guid, decimal> { [Employee] = 4m }));

        // Act
        var personDays = calculator.PersonDays(Employee, 1m);

        // Assert
        personDays.Should().Be(0.5333333333m);
    }

    [Fact]
    public void Available_ForSomeoneWithTheirOwnWorkingDay_IsStillAShareOfTheirDay()
    {
        // Arrange
        var calculator = Calculator(
            timeAway: [new TimeAwaySpan(Employee, Day, Day, 50)],
            lengths: new WorkingDayLengths(8m, new Dictionary<Guid, decimal> { [Employee] = 4m }));

        // Act
        var available = calculator.Available(Employee, OnTheTeamsCalendar);

        // Assert
        available.Should().Be(0.5m);
    }

    private static AvailabilityCalculator Calculator(
        Dictionary<Guid, Guid>? calendars = null,
        Dictionary<Guid, LocalDate[]>? holidays = null,
        Guid? defaultCalendarId = null,
        TimeAwaySpan[]? timeAway = null,
        WorkingDayLengths? lengths = null) =>
        new(
            calendars ?? [],
            (holidays ?? []).ToDictionary(c => c.Key, c => (IReadOnlySet<LocalDate>)c.Value.ToHashSet()),
            defaultCalendarId,
            timeAway ?? [],
            lengths);
}
