using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningSprints.EventHandlers;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningSprints.EventHandlers;

/// <summary>
/// <see cref="PlanningSprintSyncHandler"/> keeps the Planning copy of each sprint correct however the durable
/// <c>Iteration*</c> events arrive: late, twice, out of order, or after the sprint was deleted.
/// </summary>
public sealed class PlanningSprintSyncHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant FirstEdit = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant SecondEdit = Created.Plus(Duration.FromMinutes(10));
    private static readonly IterationDateRange Range =
        new(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14));

    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PlanningSprintSyncHandler _handler;

    public PlanningSprintSyncHandlerTests()
    {
        _handler = new PlanningSprintSyncHandler(_planningDbContext, _dispatcher.Object, Mock.Of<ILogger<PlanningSprintSyncHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new PlanningSprintFaker().WithName("Sprint 1").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == source.Id).Subject;
        copy.Name.Should().Be("Sprint 1");
        copy.DateRange.Should().Be(Range);
        copy.TeamId.Should().Be(source.TeamId);
        copy.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == id);
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetSimpleIterationQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheSprintWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithName("Old Name").WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Single(s => s.Id == id);
        copy.Name.Should().Be("New Name");
        copy.Watermarks.Details.Should().Be(FirstEdit);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenRedelivered_SavesOnce()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithName("Old Name").WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "New Name", FirstEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(DetailsUpdatedEvent(id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Single(s => s.Id == id).Name.Should().Be("New Name");
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenDeliveredOutOfOrder_KeepsTheLaterEdit()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "Sprint 1b", SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(DetailsUpdatedEvent(id, "Sprint 1a", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Single(s => s.Id == id).Name.Should().Be("Sprint 1b");
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DateRangeChangedAndDetailsUpdated_WhenDeliveredOutOfOrder_ApplyBoth()
    {
        // Arrange — a date change at FirstEdit arrives after a rename at SecondEdit; neither replaces the other.
        var id = Guid.CreateVersion7();
        var moved = new IterationDateRange(Range.Start, new LocalDate(2026, 1, 21));
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "Renamed", SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(new IterationDateRangeChangedEventV2(id, 1, Range, moved, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Single(s => s.Id == id);
        copy.Name.Should().Be("Renamed");
        copy.DateRange.Should().Be(moved);
        _planningDbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DateRangeChanged_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var moved = new IterationDateRange(Range.Start, new LocalDate(2026, 1, 21));
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationDateRangeChangedEventV2(id, 1, Range, moved, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Single(s => s.Id == id).DateRange.Should().Be(moved);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamChanged_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var teamId = Guid.NewGuid();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithTeamId(null).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationTeamChangedEvent(id, 1, null, teamId, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Single(s => s.Id == id).TeamId.Should().Be(teamId);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamChanged_WhenTheTeamIsClearedOnAMappedSprint_UnmapsIt()
    {
        // Arrange
        var (planningInterval, sprint, iterationId) = MappedSprint();

        // Act
        await _handler.Handle(new IterationTeamChangedEvent(sprint.Id, 1, sprint.TeamId, null, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        sprint.TeamId.Should().BeNull();
        planningInterval.IterationSprints.Should().BeEmpty();
        var raised = planningInterval.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Should().ContainSingle().Subject;
        raised.Removed.Should().BeEquivalentTo([new PlanningIntervalSprintMapping(iterationId, sprint.Id)]);
        raised.Timestamp.Should().Be(SecondEdit);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamChanged_WhenAMappedSprintMovesToAnotherTeam_UnmapsIt()
    {
        // Arrange
        var (planningInterval, sprint, _) = MappedSprint();
        var otherTeamId = Guid.NewGuid();

        // Act
        await _handler.Handle(new IterationTeamChangedEvent(sprint.Id, 1, sprint.TeamId, otherTeamId, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        sprint.TeamId.Should().Be(otherTeamId);
        planningInterval.IterationSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamChanged_WhenOlderThanTheCopy_LeavesTheMapping()
    {
        // Arrange
        var (planningInterval, sprint, _) = MappedSprint();
        var teamId = sprint.TeamId;

        // Act
        await _handler.Handle(new IterationTeamChangedEvent(sprint.Id, 1, null, Guid.NewGuid(), EventActor.System, Created.Minus(Duration.FromMinutes(1))), TestContext.Current.CancellationToken);

        // Assert
        sprint.TeamId.Should().Be(teamId);
        planningInterval.IterationSprints.Should().ContainSingle();
        planningInterval.DomainEvents.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

#pragma warning disable CS0618 // the retired types are exactly what is under test
    [Fact]
    public async Task Handle_RetiredStateChanged_ChangesNothing()
    {
        // Arrange — an envelope written before state stopped being stored, for a mapped sprint and a deleted one.
        var (planningInterval, sprint, _) = MappedSprint();
        SourceReturns(null);

        // Act
        await _handler.Handle(new IterationStateChangedEvent(sprint.Id, 1, IterationState.Active, IterationState.Completed, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(new IterationStateChangedEvent(Guid.CreateVersion7(), 1, IterationState.Future, IterationState.Active, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        sprint.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
        planningInterval.IterationSprints.Should().ContainSingle();
        planningInterval.DomainEvents.Should().BeEmpty();
        _planningDbContext.PlanningSprints.Should().ContainSingle();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SupersededCreatedV2_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new PlanningSprintFaker().WithName("Sprint 1").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(new IterationCreatedEventV2(source.Id, 1, "Sprint 1", IterationType.Sprint, IterationState.Active,
            Range, null, EventActor.System, Created), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == source.Id).Subject;
        copy.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededDateRangeChanged_AppliesTheUtcDateOfEachInstant()
    {
        // Arrange — an envelope written as the superseded type before the switch, still in the outbox.
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));
        var previous = new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0));
        var moved = new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 21, 0, 0));

        // Act
        await _handler.Handle(new IterationDateRangeChangedEvent(id, 1, previous, moved, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Single(s => s.Id == id).DateRange.Should().Be(new IterationDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 21)));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededCreated_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new PlanningSprintFaker().WithName("Sprint 1").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(new IterationCreatedEvent(source.Id, 1, "Sprint 1", IterationType.Sprint, IterationState.Active,
            new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0)), null, EventActor.System, Created),
            TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == source.Id).Subject;
        copy.DateRange.Should().Be(Range);
        copy.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededUpdated_AppliesEveryGroupItCarries()
    {
        // Arrange — an envelope written as the superseded type before the switch, still in the outbox.
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithName("Old Name").WithTeamId(null).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationUpdatedEvent(id, 1, "New Name", IterationType.Sprint, IterationState.Active,
            new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 28, 0, 0)), null, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Single(s => s.Id == id);
        copy.Name.Should().Be("New Name");
        copy.DateRange.End.Should().Be(new LocalDate(2026, 1, 28));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task Handle_DetailsUpdated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new PlanningSprintFaker().WithName("New Name").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == source.Id).Subject;
        copy.Name.Should().Be("New Name");
        copy.Watermarks.Should().Be(PlanningSprintWatermarks.At(FirstEdit));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationDeletedEvent(id, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheSprintIsMapped_UnmapsItFromThePlanningIntervalAndRemovesTheCopy()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31));
        var teamId = Guid.NewGuid();
        var mappingPi = new PlanningIntervalFaker().WithDateRange(piDates).WithTeams(teamId).WithIterations(piDates, 2, "Iteration ").Generate();
        var otherPi = new PlanningIntervalFaker().WithDateRange(piDates).WithTeams(teamId).WithIterations(piDates, 2, "Iteration ").Generate();

        var sprint = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).WithDateRange(Range).Generate(), Created);
        var keptSprint = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).WithDateRange(Range).Generate(), Created);
        var iterationId = mappingPi.Iterations.First().Id;
        mappingPi.MapSprintToIteration(iterationId, sprint, EventActor.System, Created).IsSuccess.Should().BeTrue();
        mappingPi.MapSprintToIteration(mappingPi.Iterations.Last().Id, keptSprint, EventActor.System, Created).IsSuccess.Should().BeTrue();
        otherPi.MapSprintToIteration(otherPi.Iterations.First().Id, keptSprint, EventActor.System, Created).IsSuccess.Should().BeTrue();
        mappingPi.ClearDomainEvents();
        otherPi.ClearDomainEvents();

        _planningDbContext.AddPlanningIntervals([mappingPi, otherPi]);
        _planningDbContext.AddPlanningSprints([sprint, keptSprint]);

        // Act
        await _handler.Handle(new IterationDeletedEvent(sprint.Id, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        mappingPi.IterationSprints.Should().ContainSingle().Which.SprintId.Should().Be(keptSprint.Id);
        var raised = mappingPi.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Should().ContainSingle().Subject;
        raised.Removed.Should().BeEquivalentTo([new PlanningIntervalSprintMapping(iterationId, sprint.Id)]);
        raised.Added.Should().BeEmpty();
        raised.Timestamp.Should().Be(SecondEdit);

        otherPi.IterationSprints.Should().ContainSingle().Which.SprintId.Should().Be(keptSprint.Id);
        otherPi.DomainEvents.Should().BeEmpty();

        _planningDbContext.PlanningSprints.Should().ContainSingle().Which.Id.Should().Be(keptSprint.Id);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new IterationDeletedEvent(Guid.CreateVersion7(), EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    /// <summary>A sprint mapped to the first iteration of a PI its team is in, with the PI's events cleared.</summary>
    private (PlanningInterval PlanningInterval, PlanningSprint Sprint, Guid IterationId) MappedSprint()
    {
        var piDates = new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31));
        var teamId = Guid.NewGuid();
        var planningInterval = new PlanningIntervalFaker().WithDateRange(piDates).WithTeams(teamId).WithIterations(piDates, 2, "Iteration ").Generate();
        var sprint = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).WithDateRange(Range).Generate(), Created);
        var iterationId = planningInterval.Iterations.First().Id;
        planningInterval.MapSprintToIteration(iterationId, sprint, EventActor.System, Created).IsSuccess.Should().BeTrue();
        planningInterval.ClearDomainEvents();

        _planningDbContext.AddPlanningInterval(planningInterval);
        _planningDbContext.AddPlanningSprint(sprint);

        return (planningInterval, sprint, iterationId);
    }

    private void SourceReturns(ISimpleIteration? sprint) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleIterationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint);

    private static IterationCreatedEventV3 CreatedEvent(Guid id) =>
        new(
            id: id,
            key: 1,
            name: "Sprint 1",
            type: IterationType.Sprint,
            dateRange: Range,
            teamId: null,
            actor: EventActor.System,
            timestamp: Created);

    private static IterationDetailsUpdatedEvent DetailsUpdatedEvent(Guid id, string name, Instant timestamp) =>
        new(id, 1, name, IterationType.Sprint, new IterationDetails("Sprint 1", IterationType.Sprint), EventActor.System, timestamp);
}
