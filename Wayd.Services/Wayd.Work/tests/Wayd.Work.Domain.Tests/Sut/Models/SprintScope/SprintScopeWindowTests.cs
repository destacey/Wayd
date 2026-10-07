using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintScope;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintScopeWindowTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly SprintSchedule ChicagoSchedule = new(Chicago, CommitmentGraceDays: 1, SizingMethod.StoryPoints);

    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint1LastDay = new(2026, 9, 25);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);

    private readonly Guid _teamId = Guid.NewGuid();

    private Iteration NewSprint(LocalDate start, LocalDate end, int key, Guid? teamId, Instant? started = null, Instant? completed = null) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(key)
            .WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(start, end))
            .WithStarted(started)
            .WithCompleted(completed)
            .Generate();

    private TeamSprintTimeline Timeline(params Iteration[] sprints) =>
        new(_teamId, sprints, new TeamSprintSchedules([new SprintSchedulePeriod(new LocalDate(2026, 1, 1), null, ChicagoSchedule)], ChicagoSchedule));

    private static Instant At(LocalDate date, int hour = 0) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    [Fact]
    public void For_SprintTheTeamDidNotStartOrComplete_UsesTheDefaults()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay, 1, _teamId);
        var sprint2 = NewSprint(Sprint2Start, Sprint2Start.PlusDays(11), 2, _teamId);

        // Act
        var result = SprintScopeWindow.For(Timeline(sprint1, sprint2), sprint1);

        // Assert
        result.Start.Should().Be(At(Sprint1Start.PlusDays(1)));
        result.End.Should().Be(At(Sprint1LastDay.PlusDays(1)));
        result.LastDay.Should().Be(At(Sprint1LastDay));
        result.NextSprintId.Should().Be(sprint2.Id);
        result.StartIsActual.Should().BeFalse();
        result.EndIsActual.Should().BeFalse();
        result.TimeZone.Should().Be(Chicago);
    }

    [Fact]
    public void For_SprintCompletedBeforeItsLastPlannedDay_TakesTheDayItEndedAsItsLastDay()
    {
        // Arrange
        var completed = At(Sprint1LastDay.PlusDays(-2), 15);
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay, 1, _teamId, started: At(Sprint1Start, 9), completed: completed);

        // Act
        var result = SprintScopeWindow.For(Timeline(sprint1), sprint1);

        // Assert
        result.End.Should().Be(completed);
        result.LastDay.Should().Be(At(Sprint1LastDay.PlusDays(-2)));
        result.StartIsActual.Should().BeTrue();
        result.EndIsActual.Should().BeTrue();
        result.NextSprintId.Should().BeNull();
    }

    [Fact]
    public void For_SprintCompletedAfterItsLastPlannedDay_KeepsTheLastPlannedDay()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay, 1, _teamId, started: At(Sprint1Start, 9), completed: At(Sprint1LastDay.PlusDays(3), 10));

        // Act
        var result = SprintScopeWindow.For(Timeline(sprint1), sprint1);

        // Assert
        result.LastDay.Should().Be(At(Sprint1LastDay));
    }

    [Fact]
    public void Unscheduled_SprintWithNoTeam_UsesTheGivenScheduleAndHasNoNextSprint()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, Sprint1LastDay, 1, null);
        var schedule = new SprintSchedule(DateTimeZone.Utc, CommitmentGraceDays: 2, SizingMethod.Count);

        // Act
        var result = SprintScopeWindow.Unscheduled(sprint, schedule);

        // Assert
        result.Start.Should().Be(Sprint1Start.PlusDays(2).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant());
        result.End.Should().Be(Sprint1LastDay.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant());
        result.LastDay.Should().Be(Sprint1LastDay.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant());
        result.NextSprintId.Should().BeNull();
        result.TimeZone.Should().Be(DateTimeZone.Utc);
    }
}
