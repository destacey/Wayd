using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Notifications;
using Wayd.Planning.Application.PlanningIntervals.EventHandlers;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.EventHandlers;

public sealed class PlanningIntervalObjectiveNotificationEventHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 16, 0);
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<INotificationWriter> _writer = new();
    private readonly PlanningTeam _team = new PlanningTeamFaker(TeamType.Team).Generate();

    public PlanningIntervalObjectiveNotificationEventHandlerTests()
    {
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamMemberIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
    }

    private PlanningIntervalObjectiveNotificationEventHandler CreateHandler() =>
        new(_dbContext, _dispatcher.Object, _writer.Object);

    /// <summary>Every event the handler notifies on, with what it should be called.</summary>
    public static TheoryData<string, NotificationEvent> NotifyingEvents() => new()
    {
        { nameof(PlanningIntervalObjectiveCreatedEvent), NotificationEvent.Created },
        { nameof(PlanningIntervalObjectiveStatusChangedEvent), NotificationEvent.StatusChanged },
        { nameof(PlanningIntervalObjectiveHealthCheckAddedEvent), NotificationEvent.HealthReported },
        { nameof(PlanningIntervalObjectiveTimelineChangedEvent), NotificationEvent.TimelineChanged },
    };

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotifyAsTheMatchingEvent(string eventType, NotificationEvent expected)
    {
        // Arrange
        var objective = AddObjective();
        var @event = Build(eventType, objective.Id);

        // Act
        await CreateHandler().Handle((dynamic)@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.Event == expected && c.OwnerId == objective.Id && c.Source == @event),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReadNothing_WhenTheChangeCannotNotifyAnyone()
    {
        // Arrange
        var objective = AddObjective();
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateHandler().Handle(StatusChanged(objective.Id), TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(d => d.Send(It.IsAny<GetTeamMemberIdsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotifyTheOwningTeamsMembers_OfAStatusChange()
    {
        // Arrange
        var objective = AddObjective();
        var members = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _dispatcher
            .Setup(d => d.Send(It.Is<GetTeamMemberIdsQuery>(q => q.TeamId == _team.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);
        var @event = StatusChanged(objective.Id);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.OwnerType == PlanningFollowOwners.PlanningIntervalObjective.Key
                && c.OwnerId == objective.Id
                && c.Event == NotificationEvent.StatusChanged
                && c.Source == @event),
            members,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotify_WhenTheObjectiveNoLongerExists()
    {
        // Act
        await CreateHandler().Handle(StatusChanged(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(d => d.Send(It.IsAny<GetTeamMemberIdsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _dbContext.Dispose();

    private static DomainEvent Build(string eventType, Guid id) => eventType switch
    {
        nameof(PlanningIntervalObjectiveCreatedEvent) => new PlanningIntervalObjectiveCreatedEvent(
            id, 7, Guid.NewGuid(), Guid.NewGuid(), "Ship it", null, PlanningIntervalObjectiveType.Team, ObjectiveStatus.NotStarted,
            0, false, null, null, null, null, Actor, Now),
        nameof(PlanningIntervalObjectiveStatusChangedEvent) => StatusChanged(id),
        nameof(PlanningIntervalObjectiveHealthCheckAddedEvent) => new PlanningIntervalObjectiveHealthCheckAddedEvent(
            id, 7, Guid.NewGuid(), HealthStatus.AtRisk, null, Now, Guid.NewGuid(), Actor, Now),
        nameof(PlanningIntervalObjectiveTimelineChangedEvent) => new PlanningIntervalObjectiveTimelineChangedEvent(
            id, 7, null, null, null, null, Actor, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
    };

    private PlanningIntervalObjective AddObjective()
    {
        var objective = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), _team, ObjectiveStatus.InProgress, false).Generate();
        _dbContext.AddPlanningIntervalObjective(objective);
        return objective;
    }

    private static PlanningIntervalObjectiveStatusChangedEvent StatusChanged(Guid objectiveId) =>
        new(objectiveId, 7, ObjectiveStatus.NotStarted, ObjectiveStatus.InProgress, null, null, Actor, Now);
}
