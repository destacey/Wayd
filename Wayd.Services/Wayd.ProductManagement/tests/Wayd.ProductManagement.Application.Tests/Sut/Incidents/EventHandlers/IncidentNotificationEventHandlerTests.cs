using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.EventHandlers;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.EventHandlers;

public sealed class IncidentNotificationEventHandlerTests : ProductCommandTestBase
{
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakeDiscussionsDbContext _discussions = new();
    private readonly Mock<INotificationWriter> _writer = new();
    private IReadOnlyCollection<NotificationRecipient>? _involved;

    public IncidentNotificationEventHandlerTests()
    {
        ProductManagementDiscussionChannels.Register();
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer
            .Setup(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()))
            .Callback<RecordChange, IReadOnlyCollection<NotificationRecipient>, CancellationToken>((_, r, _) => _involved = r);
    }

    private IncidentNotificationEventHandler CreateHandler() => new(DbContext, _discussions, _writer.Object);

    /// <summary>Every incident event the handler notifies on, with what it should be called.</summary>
    public static TheoryData<string, NotificationEvent> NotifyingEvents() => new()
    {
        { nameof(IncidentCreatedEvent), NotificationEvent.Created },
        { nameof(IncidentStatusChangedEvent), NotificationEvent.StatusChanged },
        { nameof(IncidentTimelineChangedEvent), NotificationEvent.TimelineChanged },
        { nameof(IncidentTimelineNoteAddedEvent), NotificationEvent.MessagePosted },
    };

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldTellTheCommander_AsTheMatchingEvent(string eventType, NotificationEvent expected)
    {
        // Arrange
        var commander = Guid.NewGuid();
        var incident = SeedIncident(commander);
        var @event = Build(eventType, incident);

        // Act
        await CreateHandler().Handle((dynamic)@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.OwnerType == ProductManagementFollowOwners.Incident.Key
                && c.OwnerId == incident.Id
                && c.Event == expected
                && c.Source == @event),
            It.IsAny<IReadOnlyCollection<NotificationRecipient>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _involved.Should().BeEquivalentTo(new NotificationRecipient[] { new(commander, NotificationReason.Role) });
    }

    [Fact]
    public async Task Handle_ShouldTellTheCommanderAndThoseMentioned_OfAMessage()
    {
        // Arrange
        var commander = Guid.NewGuid();
        var mentioned = Guid.NewGuid();
        var incident = SeedIncident(commander);
        var @event = Posted(incident.Id, mentioned: [mentioned]);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(
            It.Is<RecordChange>(c => c.OwnerType == ProductManagementFollowOwners.Incident.Key
                && c.OwnerId == incident.Id
                && c.Event == NotificationEvent.MessagePosted
                && c.Source == @event),
            It.IsAny<IReadOnlyCollection<NotificationRecipient>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _involved.Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(commander, NotificationReason.Role),
            new(mentioned, NotificationReason.Mentioned),
        });
    }

    [Fact]
    public async Task Handle_ShouldTellTheThreadsPeople_OfAReplyInIt()
    {
        // Arrange
        var incident = SeedIncident(commander: null);
        var starter = Guid.NewGuid();
        var earlierReplier = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var channel = DiscussionChannel.OpenForRecord(ProductManagementDiscussionChannels.Incident.Key, incident.Id, Actor, Now).Value;
        var parent = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, starter, "Rollback", "Roll back?", [], Actor, Now).Value;
        var earlier = DiscussionMessage.Reply(channel, parent, earlierReplier, "Not yet", [], Actor, Now).Value;
        var otherThread = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, elsewhere, "Comms", "Status page?", [], Actor, Now).Value;
        _discussions.AddMessages(parent, earlier, otherThread);

        // Act
        await CreateHandler().Handle(Posted(incident.Id, parentId: parent.Id), TestContext.Current.CancellationToken);

        // Assert
        _involved.Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(starter, NotificationReason.Thread),
            new(earlierReplier, NotificationReason.Thread),
        });
    }

    [Fact]
    public async Task Handle_ShouldStillNotifyAMessage_WhenTheIncidentHasNoCommander()
    {
        // Arrange
        var incident = SeedIncident(commander: null);

        // Act
        await CreateHandler().Handle(Posted(incident.Id), TestContext.Current.CancellationToken);

        // Assert
        // Its followers are still told; the writer finds them.
        _involved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAMessageOnAnotherKindOfRecord()
    {
        // Arrange
        var incident = SeedIncident(Guid.NewGuid());
        var @event = Posted(incident.Id, channelType: "ppm.project");

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNeverTellAnInternalNote()
    {
        // Arrange
        var incident = SeedIncident(Guid.NewGuid());
        var @event = Posted(incident.Id, kind: DiscussionMessageKind.InternalNote);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyAMessage_WhenTheChangeCannotNotifyAnyone()
    {
        // Arrange
        var incident = SeedIncident(Guid.NewGuid());
        var @event = Posted(incident.Id);
        _writer.Setup(w => w.ShouldNotify(@event, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyAMessage_WhenTheIncidentNoLongerExists()
    {
        // Arrange
        var @event = Posted(Guid.NewGuid());

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _writer.Verify(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Incident SeedIncident(Guid? commander)
    {
        var incident = Incident.Create(
            "Checkout unavailable", null, null, commander, Guid.CreateVersion7(), Now, null, null,
            [], Status("Open", StatusCategory.Active, ProductStatusAlias.Open), EventActor.System, Now,
            ActingEmployeeId, ActingEmployeeId).Value;
        DbContext.AddIncident(incident);
        return incident;
    }

    private static DomainEvent Build(string eventType, Incident incident) => eventType switch
    {
        nameof(IncidentCreatedEvent) => new IncidentCreatedEvent(
            incident.Id, incident.Key, incident.Title, ActingEmployeeId, ActingEmployeeId, incident.CommanderEmployeeId,
            Guid.NewGuid(), Now, null, null, ProductStatusAlias.Open, Actor, Now, null, null, null, [], []),
        nameof(IncidentStatusChangedEvent) => new IncidentStatusChangedEvent(
            incident.Id, incident.Key, Guid.NewGuid(), StatusCategory.Active, ProductStatusAlias.Open,
            Guid.NewGuid(), StatusCategory.Active, ProductStatusAlias.Responding, Actor, Now, null, Now, null, null),
        nameof(IncidentTimelineChangedEvent) => new IncidentTimelineChangedEvent(
            incident.Id, incident.Key, Now, null, null, Now - Duration.FromHours(1), null, null, Actor, Now),
        nameof(IncidentTimelineNoteAddedEvent) => new IncidentTimelineNoteAddedEvent(
            incident.Id, incident.Key, Guid.NewGuid(), Actor.EmployeeId!.Value, Now, Actor, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
    };

    private static DiscussionMessagePostedEvent Posted(
        Guid incidentId,
        DiscussionMessageKind kind = DiscussionMessageKind.Public,
        Guid? parentId = null,
        Guid[]? mentioned = null,
        // Spelled out rather than read from the descriptor, so a change to the persisted key fails here.
        string channelType = "product.incident") =>
        new(Guid.NewGuid(), Guid.NewGuid(), channelType, incidentId, parentId, kind, Actor.EmployeeId!.Value,
            mentioned ?? [], Actor, Now);
}
