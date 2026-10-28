using Moq;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.ProjectPortfolioManagement.Application.Projects.EventHandlers;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.EventHandlers;

public sealed class ProjectNotificationEventHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 15, 0);
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<INotificationWriter> _writer = new();

    public ProjectNotificationEventHandlerTests()
    {
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private ProjectNotificationEventHandler CreateHandler() => new(_dbContext, _writer.Object);

    /// <summary>Every event the handler notifies on, built for a project, with what it should be called.</summary>
    public static TheoryData<string, NotificationEvent> NotifyingEvents() => new()
    {
        { nameof(ProjectCreatedEvent), NotificationEvent.Created },
        { nameof(ProjectStatusChangedEventV2), NotificationEvent.StatusChanged },
        { nameof(ProjectLifecycleChangedEventV2), NotificationEvent.LifecycleChanged },
        { nameof(ProjectHealthCheckAddedEventV2), NotificationEvent.HealthReported },
        { nameof(ProjectTimelineChangedEventV2), NotificationEvent.TimelineChanged },
        { nameof(ProjectRolesChangedEventV2), NotificationEvent.RolesChanged },
        { nameof(ProjectReparentedEventV2), NotificationEvent.Reparented },
        { nameof(ProjectScoreRecordedEventV2), NotificationEvent.ScoreRecorded },
    };

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotifyAsTheMatchingEvent(string eventType, NotificationEvent expected)
    {
        // Arrange
        var project = AddProject(new() { [ProjectRole.Owner] = [Guid.NewGuid()] });
        var @event = Build(eventType, project);

        // Act
        await HandleAny(@event);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.Event == expected && c.OwnerId == project.Id && c.Source == @event),
            It.IsAny<IReadOnlyCollection<NotificationRecipient>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldReadNothing_WhenTheChangeCannotNotifyAnyone(string eventType, NotificationEvent _)
    {
        // Arrange
        var project = AddProject(new() { [ProjectRole.Owner] = [Guid.NewGuid()] });
        var @event = Build(eventType, project);
        _writer.Setup(w => w.ShouldNotify(@event, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await HandleAny(@event);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotifyEveryRoleHolder_OfAStatusChange()
    {
        // Arrange
        var sponsor = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var member = Guid.NewGuid();
        var project = AddProject(new()
        {
            [ProjectRole.Sponsor] = [sponsor],
            [ProjectRole.Manager] = [manager, member],
            [ProjectRole.Member] = [member],
        });
        var @event = StatusChanged(project, fromStatus: "Proposed");

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.OwnerType == PpmFollowOwners.Project.Key
                && c.OwnerId == project.Id
                && c.Event == NotificationEvent.StatusChanged
                && c.Source == @event),
            // A person holding two roles may appear twice; the writer merges them into one notification.
            It.Is<IReadOnlyCollection<NotificationRecipient>>(r => AreRoleHolders(r, sponsor, manager, member)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotify_OfTheStatusANewProjectStartsIn()
    {
        // Arrange
        var project = AddProject(new() { [ProjectRole.Owner] = [Guid.NewGuid()] });

        // Act
        await CreateHandler().Handle(StatusChanged(project, fromStatus: null), TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotifyOfATimelineChange()
    {
        // Arrange
        var owner = Guid.NewGuid();
        var project = AddProject(new() { [ProjectRole.Owner] = [owner] });
        var @event = new ProjectTimelineChangedEventV2(project.Id, project.Key, null, null, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.Event == NotificationEvent.TimelineChanged && c.OwnerId == project.Id),
            It.Is<IReadOnlyCollection<NotificationRecipient>>(r => AreRoleHolders(r, owner)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldStillNotify_WhenTheProjectHasNoRoleHolders()
    {
        // Arrange
        var project = AddProject(null);

        // Act
        await CreateHandler().Handle(StatusChanged(project, fromStatus: "Proposed"), TestContext.Current.CancellationToken);

        // Assert
        // Its followers are still told; the writer finds them.
        _writer.Verify(w => w.Notify(
            It.IsAny<RecordChange>(),
            It.Is<IReadOnlyCollection<NotificationRecipient>>(r => r.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotify_WhenTheProjectNoLongerExists()
    {
        // Arrange
        var project = new ProjectFaker().Generate();

        // Act
        await CreateHandler().Handle(StatusChanged(project, fromStatus: "Proposed"), TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _dbContext.Dispose();

    private Task HandleAny(DomainEvent @event) =>
        CreateHandler().Handle((dynamic)@event, TestContext.Current.CancellationToken);

    private static DomainEvent Build(string eventType, Project project) => eventType switch
    {
        nameof(ProjectCreatedEvent) => new ProjectCreatedEvent(
            project.Id, project.Key, project.Name, project.Description, 1, 1, null, Guid.NewGuid(), null, null, null, null, [], Actor, Now),
        nameof(ProjectStatusChangedEventV2) => StatusChanged(project, fromStatus: "Proposed"),
        nameof(ProjectLifecycleChangedEventV2) => new ProjectLifecycleChangedEventV2(
            project.Id, project.Key, Guid.NewGuid(), "Old", Guid.NewGuid(), "New", 3, 0, Actor, Now),
        nameof(ProjectHealthCheckAddedEventV2) => new ProjectHealthCheckAddedEventV2(
            project.Id, project.Key, Guid.NewGuid(), HealthStatus.AtRisk, null, Now, Guid.NewGuid(), Actor, Now),
        nameof(ProjectTimelineChangedEventV2) => new ProjectTimelineChangedEventV2(project.Id, project.Key, null, null, Actor, Now),
        nameof(ProjectRolesChangedEventV2) => new ProjectRolesChangedEventV2(project.Id, project.Key, [], [], [], Actor, Now),
        nameof(ProjectReparentedEventV2) => new ProjectReparentedEventV2(project.Id, project.Key, Guid.NewGuid(), null, Guid.NewGuid(), Actor, Now),
        nameof(ProjectScoreRecordedEventV2) => new ProjectScoreRecordedEventV2(
            project.Id, project.Key, Guid.NewGuid(), Guid.NewGuid(), "Model", 42m, 1, Guid.NewGuid(), Actor, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
    };

    private Project AddProject(Dictionary<ProjectRole, HashSet<Guid>>? roles)
    {
        var project = new ProjectFaker().WithRoles(roles).Generate();
        _dbContext.AddProject(project);
        return project;
    }

    private static ProjectStatusChangedEventV2 StatusChanged(Project project, string? fromStatus) =>
        new(Guid.NewGuid(), project.Id, project.Key,
            fromStatus, fromStatus is null ? null : LifecycleCategory.NotStarted,
            "Active", LifecycleCategory.Active,
            isBackward: false, reason: null, sequence: 1, Actor, Now);

    private static bool AreRoleHolders(IReadOnlyCollection<NotificationRecipient> involved, params Guid[] employeeIds) =>
        involved.All(r => r.Reason == NotificationReason.Role)
        && involved.Select(r => r.EmployeeId).ToHashSet().SetEquals(employeeIds);
}
