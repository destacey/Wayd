using Moq;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.ProjectPortfolioManagement.Application.Programs.EventHandlers;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs.EventHandlers;

public sealed class ProgramNotificationEventHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 15, 0);
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<INotificationWriter> _writer = new();

    public ProgramNotificationEventHandlerTests()
    {
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private ProgramNotificationEventHandler CreateHandler() => new(_dbContext, _writer.Object);

    /// <summary>Every event the handler notifies on, with what it should be called.</summary>
    public static TheoryData<string, NotificationEvent> NotifyingEvents() => new()
    {
        { nameof(ProgramCreatedEvent), NotificationEvent.Created },
        { nameof(ProgramStatusChangedEvent), NotificationEvent.StatusChanged },
        { nameof(ProgramTimelineChangedEvent), NotificationEvent.TimelineChanged },
        { nameof(ProgramRolesChangedEvent), NotificationEvent.RolesChanged },
    };

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotifyAsTheMatchingEvent(string eventType, NotificationEvent expected)
    {
        // Arrange
        var program = new ProgramFaker().Generate();
        _dbContext.AddProgram(program);
        var @event = Build(eventType, program.Id, program.Key);

        // Act
        await CreateHandler().Handle((dynamic)@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.Event == expected && c.OwnerId == program.Id && c.Source == @event),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotify_WhenTheChangeCannotNotifyAnyone()
    {
        // Arrange
        var program = new ProgramFaker().Generate();
        _dbContext.AddProgram(program);
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateHandler().Handle(new ProgramTimelineChangedEvent(program.Id, program.Key, null, null, Actor, Now), TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotifyEveryRoleHolder_OfATimelineChange()
    {
        // Arrange
        var sponsor = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var program = new ProgramFaker()
            .WithRoles(new() { [ProgramRole.Sponsor] = [sponsor], [ProgramRole.Owner] = [owner] })
            .Generate();
        _dbContext.AddProgram(program);
        var @event = new ProgramTimelineChangedEvent(program.Id, program.Key, null, null, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.OwnerType == PpmFollowOwners.Program.Key
                && c.OwnerId == program.Id
                && c.Event == NotificationEvent.TimelineChanged
                && c.Source == @event),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(sponsor) && ids.Contains(owner)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotNotify_WhenTheProgramNoLongerExists()
    {
        // Arrange
        var program = new ProgramFaker().Generate();
        var @event = new ProgramTimelineChangedEvent(program.Id, program.Key, null, null, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose() => _dbContext.Dispose();

    private static DomainEvent Build(string eventType, Guid id, int key) => eventType switch
    {
        nameof(ProgramCreatedEvent) => new ProgramCreatedEvent(id, key, "Atlas", "", 1, null, Guid.NewGuid(), null, [], Actor, Now),
        nameof(ProgramStatusChangedEvent) => new ProgramStatusChangedEvent(
            id, key, "Proposed", LifecycleCategory.NotStarted, "Active", LifecycleCategory.Active, Actor, Now),
        nameof(ProgramTimelineChangedEvent) => new ProgramTimelineChangedEvent(id, key, null, null, Actor, Now),
        nameof(ProgramRolesChangedEvent) => new ProgramRolesChangedEvent(id, key, [], [], [], Actor, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
    };
}
