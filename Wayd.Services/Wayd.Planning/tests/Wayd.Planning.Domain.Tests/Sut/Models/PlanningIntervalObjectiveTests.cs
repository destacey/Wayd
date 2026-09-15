using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Models;

public sealed class PlanningIntervalObjectiveTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.CreateVersion7());

    private readonly PlanningTeam _team = new PlanningTeamFaker(TeamType.Team).Generate();

    private PlanningIntervalObjective Existing(ObjectiveStatus status = ObjectiveStatus.NotStarted, bool isStretch = false, Action<PlanningIntervalObjectiveFaker>? configure = null)
    {
        var faker = new PlanningIntervalObjectiveFaker(Guid.CreateVersion7(), _team, status, isStretch);
        configure?.Invoke(faker);

        var objective = faker.Generate();
        objective.ClearDomainEvents();
        return objective;
    }

    private static void UpdateWith(PlanningIntervalObjective objective,
        string? name = null,
        string? description = null,
        ObjectiveStatus? status = null,
        double? progress = null,
        LocalDate? startDate = null,
        LocalDate? targetDate = null,
        bool? isStretch = null,
        Instant? timestamp = null)
    {
        var result = objective.Update(
            name ?? objective.Name,
            description ?? objective.Description,
            status ?? objective.Status,
            progress ?? objective.Progress,
            startDate ?? objective.StartDate,
            targetDate ?? objective.TargetDate,
            isStretch ?? objective.IsStretch,
            Actor,
            timestamp ?? Now);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_RaisesCreatedEventOnceTheKeyIsAssigned()
    {
        // Arrange
        var planningIntervalId = Guid.CreateVersion7();

        // Act
        var objective = PlanningIntervalObjective.Create(planningIntervalId, _team.Id, " Ship the thing ", null,
            PlanningIntervalObjectiveType.Team, true, new LocalDate(2026, 4, 6), new LocalDate(2026, 5, 29), 3, Actor, Now);

        // Assert
        objective.DomainEvents.Should().BeEmpty("the key is assigned by the first save");

        objective.SetPrivate(o => o.Key, 21);
        objective.ExecutePostPersistenceActions();

        var created = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveCreatedEvent>().Subject;
        created.Id.Should().Be(objective.Id);
        created.Key.Should().Be(21);
        created.PlanningIntervalId.Should().Be(planningIntervalId);
        created.TeamId.Should().Be(_team.Id);
        created.Name.Should().Be("Ship the thing");
        created.Status.Should().Be(ObjectiveStatus.NotStarted);
        created.Progress.Should().Be(0);
        created.IsStretch.Should().BeTrue();
        created.Order.Should().Be(3);
    }

    [Fact]
    public void Create_ThenChangedBeforeTheFirstSave_CreatedEventStillDescribesTheCreation()
    {
        // Arrange
        var objective = PlanningIntervalObjective.Create(Guid.CreateVersion7(), _team.Id, "Ship the thing", null,
            PlanningIntervalObjectiveType.Team, false, null, null, 1, Actor, Now);

        // Act
        UpdateWith(objective, status: ObjectiveStatus.InProgress, progress: 40);
        objective.UpdateOrder(2, Actor, Now);
        objective.SetPrivate(o => o.Key, 8);
        objective.ExecutePostPersistenceActions();

        // Assert
        var created = objective.DomainEvents.OfType<PlanningIntervalObjectiveCreatedEvent>().Should().ContainSingle().Subject;
        created.Status.Should().Be(ObjectiveStatus.NotStarted);
        created.Progress.Should().Be(0);
        created.Order.Should().Be(1);

        objective.DomainEvents.OfType<PlanningIntervalObjectiveStatusChangedEvent>().Should().ContainSingle().Which.Key.Should().Be(8);
        objective.DomainEvents.OfType<PlanningIntervalObjectiveOrderChangedEvent>().Should().ContainSingle().Which.Key.Should().Be(8);
    }

    [Fact]
    public void Import_RecordsTheImportedStatusAndProgressAsTheCreation()
    {
        // Arrange
        var closedDate = Now.Minus(Duration.FromDays(1));

        // Act
        var objective = PlanningIntervalObjective.Import(Guid.CreateVersion7(), _team.Id, "Ship the thing", null,
            PlanningIntervalObjectiveType.Team, ObjectiveStatus.Completed, 100, false, null, null, closedDate, null,
            EventActor.Import("user-1"), Now);
        objective.SetPrivate(o => o.Key, 2);
        objective.ExecutePostPersistenceActions();

        // Assert
        var created = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveCreatedEvent>().Subject;
        created.Status.Should().Be(ObjectiveStatus.Completed);
        created.Progress.Should().Be(100);
        created.ClosedDate.Should().Be(closedDate);
    }

    [Fact]
    public void Update_Renamed_RaisesDetailsUpdatedWithWhatItReplaced()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithName("Ship the thing"));
        var previousDescription = objective.Description;

        // Act
        UpdateWith(objective, name: "Ship the better thing");

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveDetailsUpdatedEvent>().Subject;
        raised.Name.Should().Be("Ship the better thing");
        raised.Previous.Should().Be(new PlanningIntervalObjectiveDetails("Ship the thing", previousDescription));
    }

    [Fact]
    public void Update_NameThatOnlyDiffersByWhitespace_RaisesNothing()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithName("Ship the thing"));

        // Act
        UpdateWith(objective, name: " Ship the thing ");

        // Assert
        objective.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_NothingChanged_RaisesNothing()
    {
        // Arrange
        var objective = Existing(ObjectiveStatus.InProgress, configure: f => f.WithDates(new LocalDate(2026, 4, 6), new LocalDate(2026, 5, 1)));

        // Act
        UpdateWith(objective);

        // Assert
        objective.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_Completed_RaisesStatusChangedWithTheClosedDateAtBothEnds()
    {
        // Arrange
        var objective = Existing(ObjectiveStatus.InProgress);

        // Act
        UpdateWith(objective, status: ObjectiveStatus.Completed);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveStatusChangedEvent>().Subject;
        raised.FromStatus.Should().Be(ObjectiveStatus.InProgress);
        raised.ToStatus.Should().Be(ObjectiveStatus.Completed);
        raised.PreviousClosedDate.Should().BeNull();
        raised.ClosedDate.Should().Be(Now);
    }

    [Fact]
    public void Update_ReopenedFromCompleted_ClearsTheClosedDateAndRecordsIt()
    {
        // Arrange
        var objective = Existing(ObjectiveStatus.InProgress);
        UpdateWith(objective, status: ObjectiveStatus.Completed);
        objective.ClearDomainEvents();

        // Act
        UpdateWith(objective, status: ObjectiveStatus.InProgress, timestamp: Now.Plus(Duration.FromDays(1)));

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveStatusChangedEvent>().Subject;
        raised.PreviousClosedDate.Should().Be(Now);
        raised.ClosedDate.Should().BeNull();
    }

    [Fact]
    public void Update_ProgressReported_RaisesProgressChanged()
    {
        // Arrange
        var objective = Existing(ObjectiveStatus.InProgress);

        // Act
        UpdateWith(objective, progress: 75);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveProgressChangedEvent>().Subject;
        raised.PreviousProgress.Should().Be(50);
        raised.Progress.Should().Be(75);
    }

    [Fact]
    public void Update_ProgressBeyondTheClampedMaximum_RaisesNothingWhenAlreadyComplete()
    {
        // Arrange
        var objective = Existing(ObjectiveStatus.Completed);

        // Act
        UpdateWith(objective, progress: 120);

        // Assert
        objective.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_MadeStretch_RaisesStretchChanged()
    {
        // Arrange
        var objective = Existing(isStretch: false);

        // Act
        UpdateWith(objective, isStretch: true);

        // Assert
        objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveStretchChangedEvent>()
            .Which.IsStretch.Should().BeTrue();
    }

    [Fact]
    public void Update_TargetDateMoved_RaisesTimelineChangedWithBothEnds()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithDates(new LocalDate(2026, 4, 6), new LocalDate(2026, 5, 1)));

        // Act
        UpdateWith(objective, targetDate: new LocalDate(2026, 5, 15));

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveTimelineChangedEvent>().Subject;
        raised.PreviousStartDate.Should().Be(new LocalDate(2026, 4, 6));
        raised.PreviousTargetDate.Should().Be(new LocalDate(2026, 5, 1));
        raised.StartDate.Should().Be(new LocalDate(2026, 4, 6));
        raised.TargetDate.Should().Be(new LocalDate(2026, 5, 15));
    }

    [Fact]
    public void UpdateOrder_Moved_RaisesOrderChanged()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithOrder(2));

        // Act
        objective.UpdateOrder(4, Actor, Now);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveOrderChangedEvent>().Subject;
        raised.PreviousOrder.Should().Be(2);
        raised.Order.Should().Be(4);
    }

    [Fact]
    public void UpdateOrder_Unchanged_RaisesNothing()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithOrder(2));

        // Act
        objective.UpdateOrder(2, Actor, Now);

        // Assert
        objective.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_RaisesDeletedWithTheName()
    {
        // Arrange
        var objective = Existing(configure: f => f.WithName("Ship the thing"));

        // Act
        objective.Delete(Actor, Now);

        // Assert
        var raised = objective.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlanningIntervalObjectiveDeletedEvent>().Subject;
        raised.Id.Should().Be(objective.Id);
        raised.Key.Should().Be(objective.Key);
        raised.PlanningIntervalId.Should().Be(objective.PlanningIntervalId);
        raised.Name.Should().Be("Ship the thing");
    }
}
