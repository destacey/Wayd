using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintIdealLineTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

    private static Instant At(LocalDate date, int hour = 0) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    private static SprintScopeWindow Window(Instant start, Instant end) =>
        new(Guid.NewGuid(), start, end, end, start, null, false, false, Chicago);

    private static double RemainingAt(IReadOnlyList<SprintIdealPoint> line, Instant at) =>
        line.Single(p => p.At == at).Remaining;

    [Fact]
    public void Build_CountsADaylightSavingDayAsOneDay()
    {
        // Arrange — Chicago's clocks go back on Sunday 1 November 2026, a 25-hour day
        var saturday = new LocalDate(2026, 10, 31);
        var window = Window(At(saturday), At(saturday.PlusDays(3)));

        // Act
        var line = SprintIdealLine.Build(window, SprintWorkingDays.EveryDay);

        // Assert
        RemainingAt(line, At(saturday.PlusDays(1))).Should().BeApproximately(2.0 / 3, 1e-9);
        RemainingAt(line, At(saturday.PlusDays(2))).Should().BeApproximately(1.0 / 3, 1e-9);
        line[^1].Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_CountsAPartialFirstDayInPart()
    {
        // Arrange — committed at noon on Monday, ending at the end of Tuesday
        var monday = new LocalDate(2026, 9, 14);
        var window = Window(At(monday, 12), At(monday.PlusDays(2)));

        // Act
        var line = SprintIdealLine.Build(window, SprintWorkingDays.EveryDay);

        // Assert — half of Monday and all of Tuesday: a third done by the end of Monday
        line.Select(p => p.At).Should().Equal(At(monday, 12), At(monday.PlusDays(1)), At(monday.PlusDays(2)));
        RemainingAt(line, At(monday.PlusDays(1))).Should().BeApproximately(2.0 / 3, 1e-9);
    }

    [Fact]
    public void Build_CountsAPartialLastDayInPart()
    {
        // Arrange — completed at noon on Tuesday
        var monday = new LocalDate(2026, 9, 14);
        var window = Window(At(monday), At(monday.PlusDays(1), 12));

        // Act
        var line = SprintIdealLine.Build(window, SprintWorkingDays.EveryDay);

        // Assert — all of Monday and half of Tuesday
        RemainingAt(line, At(monday.PlusDays(1))).Should().BeApproximately(1.0 / 3, 1e-9);
        line[^1].Should().Be(new SprintIdealPoint(At(monday.PlusDays(1), 12), 0));
    }

    [Fact]
    public void Build_IsFlatOnDaysOff()
    {
        // Arrange — Friday to Monday, with the weekend off
        var friday = new LocalDate(2026, 9, 18);
        var window = Window(At(friday), At(friday.PlusDays(4)));

        // Act
        var line = SprintIdealLine.Build(window, new SprintWorkingDays(WorkingWeek.MondayToFriday, []));

        // Assert
        RemainingAt(line, At(friday.PlusDays(1))).Should().BeApproximately(0.5, 1e-9);
        RemainingAt(line, At(friday.PlusDays(3))).Should().BeApproximately(0.5, 1e-9);
        line[^1].Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_WithEveryDayOff_FallsBackToEveryDay()
    {
        // Arrange — a weekend-only window for a Monday-to-Friday team
        var saturday = new LocalDate(2026, 9, 19);
        var window = Window(At(saturday), At(saturday.PlusDays(2)));

        // Act
        var line = SprintIdealLine.Build(window, new SprintWorkingDays(WorkingWeek.MondayToFriday, []));

        // Assert
        RemainingAt(line, At(saturday.PlusDays(1))).Should().BeApproximately(0.5, 1e-9);
        line[^1].Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_WhenTheWindowIsEmpty_GoesStraightFromOneToZero()
    {
        // Arrange
        var at = At(new LocalDate(2026, 9, 14), 9);
        var window = Window(at, at);

        // Act
        var line = SprintIdealLine.Build(window, SprintWorkingDays.EveryDay);

        // Assert
        line.Should().Equal(new SprintIdealPoint(at, 1), new SprintIdealPoint(at, 0));
    }
}
