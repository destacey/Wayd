using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
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

        // Assert — the start of sprint 3's first planned day, not its commitment point
        result.Should().Be(At(Sprint3Start));
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
        var result = timeline.CanStart(sprint, At(Sprint1Start.PlusDays(-3)), At(Sprint1Start.PlusDays(-3)));

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
        var result = timeline.CanStart(sprint, At(Sprint1Start.PlusDays(-3)).Minus(Duration.FromMinutes(1)), At(Sprint1Start.PlusDays(-3)).Minus(Duration.FromMinutes(1)));

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
        var result = timeline.CanStart(sprint2, At(Sprint1Start, 12), At(Sprint1Start, 12));

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
        var result = timeline.CanStart(sprint2, At(Sprint2Start.PlusDays(-3), 15), At(Sprint2Start.PlusDays(-3), 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_AfterTheNextSprintHasStarted_Fails()
    {
        // Arrange — the source plans sprint 1 to run across sprints 2 and 3
        var sprint1 = NewSprint(Sprint1Start, 1, days: 42);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint1, sprint2, sprint3);
        var now = At(Sprint3Start.PlusDays(2));

        // Act — sprint 2's default start has passed, so starting sprint 1 now would skip over it
        var result = timeline.CanStart(sprint1, now, now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between");
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
        var result = timeline.CanStart(sprint3, At(Sprint3Start, 9), At(Sprint3Start, 9));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_AtThePlannedEnd_Fails()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint2Start), At(Sprint2Start));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between");
    }

    [Fact]
    public void CanStart_BackdatedBeforeThePlannedEnd_AfterItPassed_Succeeds()
    {
        // Arrange — the team forgot to press Start and records it afterwards
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint1Start, 10), At(Sprint2Start, 9));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CanStart_InTheFuture_Fails()
    {
        // Arrange
        var sprint = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanStart(sprint, At(Sprint1Start, 11), At(Sprint1Start, 10));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void StartWindow_RunsFromThePreviousSprintsCompletionToBeforeThePlannedEnd()
    {
        // Arrange — sprint 1 started and was completed on its last Friday
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start.PlusDays(-3), 15));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint1, sprint2, sprint3);

        // Act
        var result = timeline.StartWindow(sprint2, At(Sprint3Start.PlusDays(5)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Earliest.Should().Be(At(Sprint2Start.PlusDays(-3), 15));
        result.Value.Latest.Should().Be(timeline.PlannedEnd(sprint2).Minus(Duration.FromMilliseconds(1)));
    }

    [Fact]
    public void StartWindow_WhenTheNextSprintStartsBeforeThePlannedEnd_EndsBeforeIt()
    {
        // Arrange — the source plans sprint 2 two days into sprint 3
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start.PlusDays(-3), 15));
        var sprint2 = NewSprint(Sprint2Start, 2, days: 16);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint1, sprint2, sprint3);

        // Act
        var result = timeline.StartWindow(sprint2, At(Sprint3Start.PlusDays(5)));

        // Assert
        result.Value.Latest.Should().Be(timeline.DefaultStart(sprint3).Minus(Duration.FromMilliseconds(1)));
    }

    [Fact]
    public void StartWindow_WhenAnotherSprintIsOpen_StartsAfterItsStart()
    {
        // Arrange — sprint 1 is open, started later than its default start
        var openStarted = At(Sprint1Start.PlusDays(2), 10);
        var sprint1 = NewSprint(Sprint1Start, 1, started: openStarted);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);
        var now = At(Sprint2Start, 9);

        // Act
        var result = timeline.StartWindow(sprint2, now);

        // Assert
        result.Value.Earliest.Should().Be(openStarted.Plus(Duration.FromMilliseconds(1)));
        result.Value.Latest.Should().Be(now);
    }

    [Fact]
    public void CompleteWindow_RunsFromTheStartToNow()
    {
        // Arrange
        var started = At(Sprint1Start, 10);
        var sprint = NewSprint(Sprint1Start, 1, started: started);
        var timeline = Timeline(sprint);
        var now = At(Sprint2Start, 9);

        // Act
        var result = timeline.CompleteWindow(sprint, now);

        // Assert
        result.Value.Should().Be(new InstantWindow(started.Plus(Duration.FromMilliseconds(1)), now));
    }

    [Fact]
    public void CanComplete_BeforeTheStart_FailsWithTheWindow()
    {
        // Arrange
        var started = At(Sprint1Start, 10);
        var sprint = NewSprint(Sprint1Start, 1, started: started);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.CanComplete(sprint, At(Sprint1Start, 9), At(Sprint2Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("between");
    }

    [Fact]
    public void CanStart_WhenALaterSprintHasStarted_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.CanStart(sprint1, At(Sprint2Start, 10), At(Sprint2Start, 10));

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
        var result = timeline.CanStart(sprint, At(Sprint1Start, 9), At(Sprint1Start, 9));

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
        var result = timeline.CanComplete(sprint, At(Sprint1Start, 12), At(Sprint1Start, 12));

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
        var result = timeline.CanComplete(sprint, At(Sprint1Start.PlusDays(1), 1), At(Sprint1Start.PlusDays(1), 1));

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
        var result = timeline.CanComplete(sprint2, At(Sprint3Start, 9), At(Sprint3Start, 9));

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

    private static readonly Instant AfterAllSprints = At(Sprint3Start.PlusDays(20));

    private static Dictionary<Iteration, SprintActualDates> Corrections(params (Iteration Sprint, Instant? Started, Instant? Completed)[] corrections) =>
        corrections.ToDictionary(c => c.Sprint, c => new SprintActualDates(c.Started, c.Completed));

    [Fact]
    public void ValidateCorrection_ASprintWithNoActualDates_Succeeds()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint1Start, 10), At(Sprint2Start.PlusDays(-3), 15))), AfterAllSprints);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_ACompletionPastTheNextSprintsStart_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9), completed: At(Sprint3Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint1Start, 10), At(Sprint2Start, 12))), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Correct both together");
    }

    [Fact]
    public void ValidateCorrection_ACompletionPastTheNextSprintsStart_WithTheNextStartMovedToo_Succeeds()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9), completed: At(Sprint3Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections(
            (sprint1, At(Sprint1Start, 10), At(Sprint2Start, 12)),
            (sprint2, At(Sprint2Start, 12), At(Sprint3Start, 9))), AfterAllSprints);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_ACompletionPastTheNextSprintsDefaultStart_Fails()
    {
        // Arrange — sprint 2 was completed without being started, so it began at its default start
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2, completed: At(Sprint3Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, null, At(Sprint2Start.PlusDays(4), 9))), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Correct both together");
    }

    [Fact]
    public void ValidateCorrection_ALateCompletionItLeavesAlone_DoesNotBlockIt()
    {
        // Arrange — the team completed sprint 1 on sprint 2's second day, which the live Complete allows
        var lateCompletion = At(Sprint2Start.PlusDays(1), 15);
        var sprint1 = NewSprint(Sprint1Start, 1, completed: lateCompletion);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act — only the start is corrected
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint1Start, 10), lateCompletion)), AfterAllSprints);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_AStartBeforeThePreviousSprintStarted_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint2, At(Sprint1Start, 9), null)), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_AStartAfterThePlannedEnd_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint1);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint2Start, 9), null)), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_InTheFuture_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var timeline = Timeline(sprint1);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint1Start, 10), null)), At(Sprint1Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public void ValidateCorrection_LeavingASprintOpenBeforeALaterOneWithActualDates_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9));
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, At(Sprint1Start, 10), null)), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("open");
    }

    [Fact]
    public void ValidateCorrection_MovingThePreviousStartPastTheNextSprintsStart_Fails()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9));
        var sprint3 = NewSprint(Sprint3Start, 3, started: At(Sprint2Start, 12));
        var timeline = Timeline(sprint1, sprint2, sprint3);

        // Act — clearing sprint 2's start reverts it to its default, the end of its first day, past sprint 3's start
        var result = timeline.ValidateCorrection(Corrections((sprint2, null, null)), AfterAllSprints);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain($"{sprint2.Name} can't start after {sprint3.Name} started");
    }

    [Fact]
    public void ValidateCorrection_ClearingValues_RevertsToTheDefaults()
    {
        // Arrange
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections((sprint1, null, null)), AfterAllSprints);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateCorrection_DoesNotRecheckSprintsLeftAsTheyAre()
    {
        // Arrange — the source moved sprint 1's planned dates after it was started
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start.PlusDays(-10), 10), completed: At(Sprint2Start, 9));
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var result = timeline.ValidateCorrection(Corrections(
            (sprint1, sprint1.Started, sprint1.Completed),
            (sprint2, At(Sprint2Start, 9), At(Sprint3Start, 9))), AfterAllSprints);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void StateAt_WhenNotStarted_IsActiveFromTheStartOfTheFirstPlannedDayInTheTeamsZone()
    {
        // Arrange
        var sprint = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint);

        // Act
        var before = timeline.StateAt(sprint, At(Sprint2Start).Minus(Duration.FromMinutes(1)));
        var from = timeline.StateAt(sprint, At(Sprint2Start));

        // Assert
        before.Should().Be(IterationState.Future);
        from.Should().Be(IterationState.Active);
    }

    [Fact]
    public void StateAt_WhenTheTeamStartedOnTheSecondDay_IsFutureOnTheFirstAndActiveOnTheSecond()
    {
        // Arrange — planned from Monday, started on Tuesday morning
        var started = At(Sprint2Start.PlusDays(1), 9);
        var sprint = NewSprint(Sprint2Start, 2, started: started);
        var timeline = Timeline(sprint);

        // Act
        var monday = timeline.StateAt(sprint, At(Sprint2Start, 12));
        var tuesday = timeline.StateAt(sprint, started);

        // Assert
        monday.Should().Be(IterationState.Future);
        tuesday.Should().Be(IterationState.Active);
    }

    [Fact]
    public void StateAt_WhenTheTeamStartedEarly_IsActiveFromItsStart()
    {
        // Arrange — the previous sprint is left to end on its own, and this one is started the Friday before
        var started = At(Sprint2Start.PlusDays(-3), 15);
        var sprint1 = NewSprint(Sprint1Start, 1);
        var sprint2 = NewSprint(Sprint2Start, 2, started: started);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var state1 = timeline.StateAt(sprint1, started);
        var state2 = timeline.StateAt(sprint2, started);

        // Assert
        state1.Should().Be(IterationState.Completed);
        state2.Should().Be(IterationState.Active);
    }

    [Fact]
    public void StateAt_IsActiveForTheWholeOfTheLastPlannedDay()
    {
        // Arrange
        var sprint = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint);
        var lastDay = Sprint3Start.PlusDays(-1);

        // Act
        var lateOnTheLastDay = timeline.StateAt(sprint, At(lastDay, 23));
        var afterTheLastDay = timeline.StateAt(sprint, At(Sprint3Start));

        // Assert
        lateOnTheLastDay.Should().Be(IterationState.Active);
        afterTheLastDay.Should().Be(IterationState.Completed);
    }

    [Fact]
    public void StateAt_WhenCompletedEarly_IsCompletedFromTheCompletion()
    {
        // Arrange
        var completed = At(Sprint3Start.PlusDays(-3), 15);
        var sprint = NewSprint(Sprint2Start, 2, started: At(Sprint2Start, 9), completed: completed);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.StateAt(sprint, completed);

        // Assert
        result.Should().Be(IterationState.Completed);
    }

    [Fact]
    public void StateAt_WhenThePreviousSprintEndsLate_TheNextIsFutureUntilItDoes()
    {
        // Arrange — sprint 1 was completed mid-morning on sprint 2's first planned day
        var sprint1Completed = At(Sprint2Start, 10);
        var sprint1 = NewSprint(Sprint1Start, 1, started: At(Sprint1Start, 9), completed: sprint1Completed);
        var sprint2 = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint1, sprint2);

        // Act
        var beforeIt = timeline.StateAt(sprint2, At(Sprint2Start, 9));
        var fromIt = timeline.StateAt(sprint2, sprint1Completed);

        // Assert
        beforeIt.Should().Be(IterationState.Future);
        fromIt.Should().Be(IterationState.Active);
    }

    [Fact]
    public void ActiveFrom_WhenNotStarted_IsTheStartOfTheFirstPlannedDayInTheTeamsZone()
    {
        // Arrange
        var sprint = NewSprint(Sprint2Start, 2);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.ActiveFrom(sprint);

        // Assert
        result.Should().Be(At(Sprint2Start));
    }

    [Fact]
    public void ActiveFrom_WhenStarted_IsTheActualStart()
    {
        // Arrange
        var started = At(Sprint2Start.PlusDays(1), 14);
        var sprint = NewSprint(Sprint2Start, 2, started: started);
        var timeline = Timeline(sprint);

        // Act
        var result = timeline.ActiveFrom(sprint);

        // Assert
        result.Should().Be(started);
    }

    [Fact]
    public void StateAt_WhenTheSourcePlansTheSprintsToOverlap_OnlyOneIsActive()
    {
        // Arrange — the source plans sprint 2 two days into sprint 3
        var sprint2 = NewSprint(Sprint2Start, 2, days: 16);
        var sprint3 = NewSprint(Sprint3Start, 3);
        var timeline = Timeline(sprint2, sprint3);
        var beforeTheOverlap = At(Sprint3Start).Minus(Duration.FromMinutes(1));
        var duringTheOverlap = At(Sprint3Start, 12);

        // Act
        var before2 = timeline.StateAt(sprint2, beforeTheOverlap);
        var before3 = timeline.StateAt(sprint3, beforeTheOverlap);
        var during2 = timeline.StateAt(sprint2, duringTheOverlap);
        var during3 = timeline.StateAt(sprint3, duringTheOverlap);

        // Assert — sprint 2 hands over at the start of sprint 3's first planned day
        before2.Should().Be(IterationState.Active);
        before3.Should().Be(IterationState.Future);
        during2.Should().Be(IterationState.Completed);
        during3.Should().Be(IterationState.Active);
        timeline.ActiveFrom(sprint3).Should().Be(timeline.EffectiveEnd(sprint2));
    }
}
