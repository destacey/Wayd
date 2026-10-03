using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class TeamSprintTimelineTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly SprintSchedule ChicagoSchedule = new(Chicago, CommitmentGraceDays: 1);
    private static readonly SprintSchedule UtcSchedule = new(DateTimeZone.Utc, CommitmentGraceDays: 2);

    private readonly Guid _teamId = Guid.NewGuid();

    // Three back-to-back two-week sprints.
    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);
    private static readonly LocalDate Sprint3Start = new(2026, 10, 12);

    private Iteration NewSprint(LocalDate start, int key, int days = 14, Instant? started = null, Instant? completed = null) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(key)
            .WithTeamId(_teamId)
            .WithDateRange(new IterationDateRange(start, start.PlusDays(days - 1)))
            .WithStarted(started)
            .WithCompleted(completed)
            .Generate();

    private TeamSprintTimeline Timeline(params Iteration[] sprints) =>
        new(_teamId, sprints, new TeamSprintSchedules([new SprintSchedulePeriod(new LocalDate(2026, 1, 1), null, ChicagoSchedule)], UtcSchedule));

    private static Instant At(LocalDate date, int hour = 0) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    [Fact]
    public void DefaultStart_IsTheEndOfTheGracePeriodInTheTeamsZone()
    {
        // Arrange
        var sprint = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.DefaultStart(sprint);

        // Assert
        result.Should().Be(At(Sprint2Start.PlusDays(1)));
    }

    [Fact]
    public void ScheduleFor_WhenTheTeamHadNoScheduleThatDay_UsesTheFallback()
    {
        // Arrange
        var sprint = NewSprint(new LocalDate(2025, 12, 1), 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.ScheduleFor(sprint);

        // Assert
        result.Should().Be(UtcSchedule);
    }

    [Fact]
    public void EffectiveStart_WhenStarted_IsTheActualStart()
    {
        // Arrange
        var started = At(Sprint2Start.PlusDays(-3), 15);
        var sprint = NewSprint(Sprint2Start, 2, started: started);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.EffectiveStart(sprint);

        // Assert
        result.Should().Be(started);
    }

    [Fact]
    public void EffectiveEnd_WhenNotCompleted_IsTheEndOfTheLastPlannedDay()
    {
        // Arrange
        var sprint = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.EffectiveEnd(sprint);

        // Assert
        result.Should().Be(At(Sprint3Start));
    }

    [Fact]
    public void EffectiveEnd_WhenCompleted_IsTheActualCompletion()
    {
        // Arrange
        var completed = At(Sprint3Start.PlusDays(-3), 15);
        var sprint = NewSprint(Sprint2Start, 2, completed: completed);
        var timeline = Timeline(sprint, NewSprint(Sprint3Start, 3));

        // Act
        var result = timeline.EffectiveEnd(sprint);

        // Assert
        result.Should().Be(completed);
    }

    [Fact]
    public void EffectiveEnd_WhenTheNextSprintStartsFirst_IsCutToItsStart()
    {
        // Arrange — the source plans sprint 2 two days into sprint 3
        var sprint2 = NewSprint(Sprint2Start, 2, days: 16);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint2, sprint3);

        // Act
        var result = timeline.EffectiveEnd(sprint2);

        // Assert
        result.Should().Be(timeline.DefaultStart(sprint3));
        timeline.OverlapsNext(sprint2).Should().BeTrue();
        timeline.OverlapsPrevious(sprint3).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_WhenSprintsAreBackToBack_IsFalse()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var overlapsNext = timeline.OverlapsNext(sprint1);
        var overlapsPrevious = timeline.OverlapsPrevious(sprint2);

        // Assert
        overlapsNext.Should().BeFalse();
        overlapsPrevious.Should().BeFalse();
    }

    [Fact]
    public void CanStart_TheFirstSprintWithinThreeDaysOfItsPlannedStart_Succeeds()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint1Start.PlusDays(-3)));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_TheFirstSprintEarlierThanThreeDaysBefore_Fails()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint1Start.PlusDays(-3)).Minus(Duration.FromMinutes(1)));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanStart_BeforeThePreviousSprintHasStarted_Fails()
    {
        // Arrange — sprint 1's default start is the end of its first day
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanStart(sprint2, At(Sprint1Start, 12));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("previous sprint");
    }

    [Fact]
    public void CanStart_TheNextSprintOnTheFridayBeforeItsPlannedStart_Succeeds()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanStart(sprint2, At(Sprint2Start.PlusDays(-3), 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_ASprintTwoBehindTheOneInEffect_Fails()
    {
        // Arrange — the source plans sprint 1 to run across sprints 2 and 3
        var sprint1 = NewSprint(Sprint1Start, 1, days: 42);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint1, sprint2, sprint3);

        // Act — sprint 3's default start has passed, so it is the one in effect
        var result = timeline.CanStart(sprint1, At(Sprint3Start.PlusDays(2)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("next sprint");
    }

    [Fact]
    public void CanStart_WhenASkippedSprintsDefaultStartHasPassed_AllowsTheOneAfterIt()
    {
        // Arrange — the team never started sprint 2, so it took its default start
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint1, sprint2, sprint3);

        // Act
        var result = timeline.CanStart(sprint3, At(Sprint3Start, 9));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_AfterThePlannedEnd_Fails()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint2Start));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("planned end");
    }

    [Fact]
    public void CanStart_WhenALaterSprintHasStarted_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanStart(sprint1, At(Sprint2Start, 10));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanStart_WhenTheTeamIsNotMapped_Fails()
    {
        // Arrange
        var sprint = new IterationFaker()
            .AsSprint()
            .WithTeamId(null)
            .WithDateRange(new IterationDateRange(Sprint1Start, Sprint1Start.PlusDays(13)))
            .Generate();
        var timeline = Timeline();

        // Act
        var result = timeline.CanStart(sprint, At(Sprint1Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not mapped");
    }

    [Fact]
    public void CanComplete_BeforeTheDefaultStart_Fails()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanComplete(sprint, At(Sprint1Start, 12));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanComplete_AfterTheDefaultStart_Succeeds()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanComplete(sprint, At(Sprint1Start.PlusDays(1), 1));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanComplete_WhenAnEarlierSprintIsOpen_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanComplete(sprint2, At(Sprint3Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("earlier sprint");
    }

    [Fact]
    public void CanReopen_WhenALaterSprintHasStarted_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanReopen(sprint1);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanReopen_WhenTheTeamHasNotMovedOn_Succeeds()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanReopen(sprint1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
