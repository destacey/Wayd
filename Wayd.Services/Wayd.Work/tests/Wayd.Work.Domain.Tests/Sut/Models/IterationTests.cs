using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class IterationTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly IterationFaker _faker = new();

    public IterationTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    [Fact]
    public void Update_WhenOnlyTheNameChanges_RaisesOnlyTheDetailsEvent()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previous = new IterationDetails(iteration.Name, iteration.Type);

        // Act
        var result = iteration.Update("Sprint 42 ", iteration.Type, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.Name.Should().Be("Sprint 42");
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationDetailsUpdatedEvent>().Subject;
        raised.Id.Should().Be(iteration.Id);
        raised.Key.Should().Be(iteration.Key);
        raised.Name.Should().Be("Sprint 42");
        raised.Type.Should().Be(iteration.Type);
        raised.Previous.Should().Be(previous);
    }

    [Fact]
    public void Update_WhenTheDatesMove_RaisesTheDateRangeEventWithBothEnds()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previous = iteration.DateRange;
        var moved = new IterationDateRange(previous.Start, previous.EffectiveEnd.PlusDays(7));

        // Act
        iteration.Update(iteration.Name, iteration.Type, moved, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationDateRangeChangedEventV2>().Subject;
        raised.PreviousDateRange.Should().Be(previous);
        raised.DateRange.Should().Be(moved);
    }

    [Fact]
    public void Update_WhenTheTeamChanges_RaisesTheTeamEventWithBothEnds()
    {
        // Arrange
        var iteration = _faker.Generate();
        var previousTeamId = iteration.TeamId;
        var teamId = Guid.NewGuid();

        // Act
        iteration.Update(iteration.Name, iteration.Type, iteration.DateRange, teamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var raised = iteration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IterationTeamChangedEvent>().Subject;
        raised.PreviousTeamId.Should().Be(previousTeamId);
        raised.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Update_WhenEverythingChanges_RaisesOneEventPerPart()
    {
        // Arrange
        var iteration = _faker.Generate();
        var moved = new IterationDateRange(iteration.DateRange.Start, iteration.DateRange.EffectiveEnd.PlusDays(7));
        var type = iteration.Type == IterationType.Sprint ? IterationType.Iteration : IterationType.Sprint;

        // Act
        iteration.Update("Renamed", type, moved, Guid.NewGuid(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(IterationDetailsUpdatedEvent),
            typeof(IterationDateRangeChangedEventV2),
            typeof(IterationTeamChangedEvent));
    }

    [Fact]
    public void Update_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange — the values a sync sends back, before the setter trims the name
        var iteration = _faker.Generate();

        // Act
        var result = iteration.Update($"{iteration.Name} ", iteration.Type, iteration.DateRange, iteration.TeamId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_RaisesDeletedEvent()
    {
        // Arrange
        var iteration = _faker.Generate();

        // Act
        iteration.Delete(EventActor.Sync(null), _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<IterationDeletedEvent>()
            .Which.Id.Should().Be(iteration.Id);
    }

    [Fact]
    public void Update_BeforeTheFirstSave_WaitsForTheKey()
    {
        // Arrange
        var range = new IterationDateRange(_dateTimeProvider.Today, _dateTimeProvider.Today.PlusDays(13));
        var iteration = Iteration.Create("Sprint 1", IterationType.Sprint, range, null,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, _dateTimeProvider.Now);

        // Act
        iteration.Update("Sprint 1a", IterationType.Sprint, range, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        iteration.DomainEvents.Should().BeEmpty();
        iteration.ExecutePostPersistenceActions();
        iteration.DomainEvents.OfType<IterationCreatedEventV3>().Should().ContainSingle().Which.Name.Should().Be("Sprint 1");
        iteration.DomainEvents.OfType<IterationDetailsUpdatedEvent>().Should().ContainSingle().Which.Name.Should().Be("Sprint 1a");
    }

    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);

    private static Instant InChicago(LocalDate date, int hour) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    private static (Iteration Sprint1, Iteration Sprint2, TeamSprintTimeline Timeline) TwoSprints(Instant? sprint1Started = null, Instant? sprint1Completed = null)
    {
        var teamId = Guid.NewGuid();
        var sprint1 = new IterationFaker().AsSprint().WithKey(1).WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(Sprint1Start, Sprint1Start.PlusDays(13)))
            .WithStarted(sprint1Started).WithCompleted(sprint1Completed).Generate();
        var sprint2 = new IterationFaker().AsSprint().WithKey(2).WithTeamId(teamId)
            .WithDateRange(new IterationDateRange(Sprint2Start, Sprint2Start.PlusDays(13))).Generate();
        var schedules = new TeamSprintSchedules([new SprintSchedulePeriod(new LocalDate(2026, 1, 1), null, new SprintSchedule(Chicago, 1, SizingMethod.Count))], new SprintSchedule(DateTimeZone.Utc, 1, SizingMethod.Count));

        return (sprint1, sprint2, new TeamSprintTimeline(teamId, [sprint1, sprint2], schedules));
    }

    [Fact]
    public void Start_OnTheFridayBeforeAHoliday_AtTheMomentTheOpenSprintCompleted_Succeeds()
    {
        // Arrange — Monday is a holiday, so the team plans on Friday afternoon
        var (sprint1, sprint2, timeline) = TwoSprints(sprint1Started: InChicago(Sprint1Start, 10));
        var friday = InChicago(Sprint2Start.PlusDays(-3), 15);
        sprint1.Complete(timeline, friday, EventActor.System, friday).IsSuccess.Should().BeTrue();

        // Act
        var result = sprint2.Start(timeline, friday, EventActor.System, friday);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sprint2.Started.Should().Be(friday);
        sprint1.Completed.Should().Be(friday);
        sprint1.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintCompletedEvent>()
            .Which.Completed.Should().Be(friday);
        sprint2.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintStartedEvent>()
            .Which.Started.Should().Be(friday);
    }

    [Fact]
    public void Start_WhileAnotherSprintIsOpen_FailsAndChangesNothing()
    {
        // Arrange
        var (sprint1, sprint2, timeline) = TwoSprints(sprint1Started: InChicago(Sprint1Start, 10));

        // Act
        var result = sprint2.Start(timeline, InChicago(Sprint2Start, 9), EventActor.System, InChicago(Sprint2Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(sprint1.Name);
        sprint1.Completed.Should().BeNull();
        sprint2.Started.Should().BeNull();
        sprint1.DomainEvents.Should().BeEmpty();
        sprint2.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Start_WhenTheRulesRefuse_RaisesNothing()
    {
        // Arrange — sprint 1 has not reached its default start
        var (_, sprint2, timeline) = TwoSprints();

        // Act
        var result = sprint2.Start(timeline, InChicago(Sprint1Start, 9), EventActor.System, InChicago(Sprint1Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        sprint2.Started.Should().BeNull();
        sprint2.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Complete_RecordsTheInstantAndRaisesTheCompletedEvent()
    {
        // Arrange
        var (sprint1, _, timeline) = TwoSprints(sprint1Started: InChicago(Sprint1Start, 10));
        var now = InChicago(Sprint2Start, 9);

        // Act
        var result = sprint1.Complete(timeline, now, EventActor.System, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sprint1.Completed.Should().Be(now);
        sprint1.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintCompletedEvent>()
            .Which.Completed.Should().Be(now);
    }

    [Fact]
    public void Reopen_ClearsTheCompletionAndCarriesIt()
    {
        // Arrange
        var completed = InChicago(Sprint2Start, 9);
        var (sprint1, _, timeline) = TwoSprints(sprint1Started: InChicago(Sprint1Start, 10), sprint1Completed: completed);

        // Act
        var result = sprint1.Reopen(timeline, EventActor.System, InChicago(Sprint2Start, 10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        sprint1.Completed.Should().BeNull();
        sprint1.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintReopenedEvent>()
            .Which.PreviousCompleted.Should().Be(completed);
    }

    [Fact]
    public void CorrectActualDates_RecordsTheDatesAndCarriesBothEnds()
    {
        // Arrange — a past sprint the team never started or completed
        var (sprint1, _, timeline) = TwoSprints();
        var started = InChicago(Sprint1Start, 10);
        var completed = InChicago(Sprint2Start.PlusDays(-3), 15);
        var now = InChicago(Sprint2Start.PlusDays(5), 9);

        var correction = timeline.ValidateCorrection(new Dictionary<Iteration, SprintActualDates> { [sprint1] = new(started, completed) }, now).Value;

        // Act
        sprint1.CorrectActualDates(correction, EventActor.System, now);

        // Assert
        sprint1.Started.Should().Be(started);
        sprint1.Completed.Should().Be(completed);
        var correctedEvent = sprint1.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintActualDatesCorrectedEvent>().Subject;
        correctedEvent.Previous.Should().Be(new SprintActualDates(null, null));
        correctedEvent.Current.Should().Be(new SprintActualDates(started, completed));
    }

    [Fact]
    public void CorrectActualDates_WhenUnchanged_RaisesNothing()
    {
        // Arrange
        var started = InChicago(Sprint1Start, 10);
        var (sprint1, _, timeline) = TwoSprints(sprint1Started: started);
        var now = InChicago(Sprint1Start, 12);
        var correction = timeline.ValidateCorrection(new Dictionary<Iteration, SprintActualDates> { [sprint1] = new(started, null) }, now).Value;

        // Act
        sprint1.CorrectActualDates(correction, EventActor.System, now);

        // Assert
        sprint1.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CorrectActualDates_ForASprintTheCorrectionLeavesOut_Throws()
    {
        // Arrange
        var (sprint1, sprint2, timeline) = TwoSprints();
        var now = InChicago(Sprint2Start, 9);
        var correction = timeline.ValidateCorrection(new Dictionary<Iteration, SprintActualDates> { [sprint1] = new(InChicago(Sprint1Start, 10), null) }, now).Value;

        // Act
        var act = () => sprint2.CorrectActualDates(correction, EventActor.System, now);

        // Assert
        act.Should().Throw<ArgumentException>();
        sprint2.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CompleteOnTeamMove_CompletesAnOpenSprintAndKeepsItsStart()
    {
        // Arrange
        var started = InChicago(Sprint1Start, 10);
        var (sprint1, _, _) = TwoSprints(sprint1Started: started);
        var now = InChicago(Sprint1Start.PlusDays(2), 9);

        // Act
        var result = sprint1.CompleteOnTeamMove(EventActor.System, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sprint1.Started.Should().Be(started);
        sprint1.Completed.Should().Be(now);
        sprint1.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SprintCompletedEvent>();
    }

    [Fact]
    public void CompleteOnTeamMove_WhenTheSprintIsNotOpen_FailsAndRaisesNothing()
    {
        // Arrange
        var (sprint1, _, _) = TwoSprints();

        // Act
        var result = sprint1.CompleteOnTeamMove(EventActor.System, InChicago(Sprint1Start, 9));

        // Assert
        result.IsFailure.Should().BeTrue();
        sprint1.DomainEvents.Should().BeEmpty();
    }
}
