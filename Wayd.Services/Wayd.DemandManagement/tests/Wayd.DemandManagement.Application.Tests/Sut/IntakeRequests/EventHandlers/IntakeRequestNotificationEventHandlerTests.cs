using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.EventHandlers;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.EventHandlers;

public sealed class IntakeRequestNotificationEventHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 15, 0);
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<INotificationWriter> _writer = new();
    private readonly List<(RecordChange Change, IReadOnlyCollection<NotificationRecipient> Involved)> _notified = [];

    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _submitter = Guid.NewGuid();
    private readonly Guid _assignee = Guid.NewGuid();
    private readonly Guid _participant = Guid.NewGuid();

    public IntakeRequestNotificationEventHandlerTests()
    {
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()))
            .Callback((RecordChange change, IReadOnlyCollection<NotificationRecipient> involved, CancellationToken _) => _notified.Add((change, involved)))
            .ReturnsAsync(1);
    }

    private IntakeRequestNotificationEventHandler CreateHandler() => new(_dbContext, _writer.Object);

    /// <summary>
    /// Every event the handler notifies on, with what it should be called. The tier is the event's own, which the
    /// read rule must hold the notification to.
    /// </summary>
    public static TheoryData<string, NotificationEvent> NotifyingEvents() => new()
    {
        { nameof(IntakeRequestStatusChangedEvent), NotificationEvent.StatusChanged },
        { nameof(IntakeRequestWithdrawnEvent), NotificationEvent.Withdrawn },
        { nameof(IntakeRequestRetitledEvent), NotificationEvent.Retitled },
        { nameof(IntakeRequestDetailsUpdatedEvent), NotificationEvent.DetailsChanged },
        { nameof(IntakeRequestNeedByChangedEvent), NotificationEvent.TimelineChanged },
        { nameof(IntakeRequestAssigneeChangedEvent), NotificationEvent.AssigneeChanged },
        { nameof(IntakeRequestReroutedEvent), NotificationEvent.Reparented },
        { nameof(IntakeRequestParticipantAddedEvent), NotificationEvent.ParticipantsChanged },
        { nameof(IntakeRequestParticipantRemovedEvent), NotificationEvent.ParticipantsChanged },
        { nameof(IntakeRequestChannelChangedEventV2), NotificationEvent.ChannelChanged },
    };

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotifyAsTheMatchingEvent(string eventType, NotificationEvent expected)
    {
        // Arrange
        var request = AddRequest();
        var @event = Build(eventType, request);

        // Act
        await HandleAny(@event);

        // Assert
        var (change, _) = _notified.Should().ContainSingle().Subject;
        change.Should().Be(new RecordChange(DemandManagementFollowOwners.IntakeRequest.Key, request.Id, expected, @event));
    }

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public void NotificationTier_ShouldMatchTheTierOfTheEventItStandsFor(string eventType, NotificationEvent notificationEvent)
    {
        // Arrange
        var eventTier = (IntakeRequestReadTier)typeof(IIntakeRequestEvent).Assembly
            .GetTypes().Single(t => t.Name == eventType)
            .GetProperty(nameof(IIntakeRequestEvent.ReadTier))!
            .GetValue(null)!;

        // Act
        var notificationTier = IntakeRequestNotificationTiers.TierOf(notificationEvent);

        // Assert
        // Looser and the notification tells a reader of a change they cannot see on the request.
        notificationTier.Should().Be(eventTier);
    }

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotNotify_WhenTheChangeCannotNotifyAnyone(string eventType, NotificationEvent _)
    {
        // Arrange
        var request = AddRequest();
        var @event = Build(eventType, request);
        _writer.Setup(w => w.ShouldNotify(@event, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await HandleAny(@event);

        // Assert
        _notified.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(NotifyingEvents))]
    public async Task Handle_ShouldNotNotify_WhenTheRequestWasDeletedSince(string eventType, NotificationEvent _)
    {
        // Arrange — built for a request that is not in the store
        var @event = Build(eventType, new IntakeRequestFaker().Generate());

        // Act
        await HandleAny(@event);

        // Assert
        _notified.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldTellEachPersonOnTheRequestForTheirOwnReason_OfAStatusChange()
    {
        // Arrange
        var request = AddRequest();

        // Act
        await CreateHandler().Handle(StatusChanged(request), TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_requester, NotificationReason.Requester),
            new(_assignee, NotificationReason.Assigned),
            new(_participant, NotificationReason.Participant),
            new(_submitter, NotificationReason.Participant),
        });
    }

    [Fact]
    public async Task Handle_ShouldNotTellTheRequesterTwice_WhenTheySubmittedItThemselves()
    {
        // Arrange
        var request = AddRequest(submittedById: _requester);

        // Act
        await CreateHandler().Handle(StatusChanged(request), TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().ContainSingle(r => r.EmployeeId == _requester)
            .Which.Reason.Should().Be(NotificationReason.Requester);
    }

    [Fact]
    public async Task Handle_ShouldTellNoAssignee_WhenTheRequestIsUnassigned()
    {
        // Arrange
        var request = AddRequest(unassigned: true);

        // Act
        await CreateHandler().Handle(StatusChanged(request), TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().NotContain(r => r.Reason == NotificationReason.Assigned);
    }

    [Fact]
    public async Task Handle_ShouldTellTheAssigneeItHadAndTheOneItHasNow_OfAnAssigneeChange()
    {
        // Arrange
        var previous = Guid.NewGuid();
        var request = AddRequest();
        var @event = new IntakeRequestAssigneeChangedEvent(request.Id, request.Key, previous, _assignee, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        // The new assignee is also the current one; the writer merges the repeat into one notification.
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_requester, NotificationReason.Requester),
            new(_assignee, NotificationReason.Assigned),
            new(previous, NotificationReason.Assigned),
            new(_assignee, NotificationReason.Assigned),
            new(_participant, NotificationReason.Participant),
            new(_submitter, NotificationReason.Participant),
        });
    }

    [Fact]
    public async Task Handle_ShouldReadTheAssigneeAsTheRequestIsNow_NotAsItWas()
    {
        // Arrange — reassigned after the status change was raised; the earlier assignee is no longer told
        var request = AddRequest();
        var @event = StatusChanged(request);
        var newAssignee = Guid.NewGuid();
        request.ChangeAssignee(newAssignee, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Where(r => r.Reason == NotificationReason.Assigned).Select(r => r.EmployeeId)
            .Should().Equal(newAssignee);
    }

    [Fact]
    public async Task Handle_ShouldTellTheAssigneeAndThePersonAdded_OfAParticipantAdded()
    {
        // Arrange
        var added = Guid.NewGuid();
        var request = AddRequest();
        var @event = new IntakeRequestParticipantAddedEvent(request.Id, request.Key, added, [_participant, added], Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert — not the requester, the submitter or the other participants
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_assignee, NotificationReason.Assigned),
            new(added, NotificationReason.Participant),
        });
    }

    [Theory]
    [InlineData(IntakeDeskVisibility.Open, true)]
    [InlineData(IntakeDeskVisibility.SummaryOnly, false)]
    [InlineData(IntakeDeskVisibility.Private, false)]
    public async Task Handle_ShouldTellThePersonRemoved_OnlyOnAnOpenDesk(IntakeDeskVisibility visibility, bool toldRemoved)
    {
        // Arrange — off an open desk the person removed can no longer read who takes part, so could never see it
        var desk = new IntakeDeskFaker().WithVisibility(visibility).Generate();
        _dbContext.AddDesks(desk);
        var removed = Guid.NewGuid();
        var request = AddRequest(deskId: desk.Id);
        var @event = new IntakeRequestParticipantRemovedEvent(request.Id, request.Key, removed, [_participant], Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        NotificationRecipient[] expected = toldRemoved
            ? [new(_assignee, NotificationReason.Assigned), new(removed, NotificationReason.Participant)]
            : [new(_assignee, NotificationReason.Assigned)];
        Involved().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_ShouldTellTheRequesterAndTheAssignee_OfAChannelChange()
    {
        // Arrange
        var request = AddRequest();
        var @event = new IntakeRequestChannelChangedEventV2(request.Id, request.Key, Guid.NewGuid(), Guid.NewGuid(), Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_requester, NotificationReason.Requester),
            new(_assignee, NotificationReason.Assigned),
        });
    }

    private IReadOnlyCollection<NotificationRecipient> Involved() => _notified.Should().ContainSingle().Subject.Involved;

    private IntakeRequest AddRequest(Guid? submittedById = null, bool unassigned = false, Guid? deskId = null)
    {
        var request = new IntakeRequestFaker()
            .WithDeskId(deskId ?? Guid.NewGuid())
            .WithRequesterId(_requester)
            .WithSubmittedById(submittedById ?? _submitter)
            .WithAssigneeId(unassigned ? null : _assignee)
            .WithParticipants(_participant)
            .Generate();
        _dbContext.AddRequests(request);
        return request;
    }

    private static IntakeRequestStatusChangedEvent StatusChanged(IntakeRequest request) =>
        new(request.Id, request.Key,
            Guid.NewGuid(), StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted,
            Guid.NewGuid(), StatusCategory.Active, IntakeRequestStatusAlias.Accepted,
            null, null, Actor, Now);

    private static DomainEvent Build(string eventType, IntakeRequest request) => eventType switch
    {
        nameof(IntakeRequestStatusChangedEvent) => StatusChanged(request),
        nameof(IntakeRequestWithdrawnEvent) => new IntakeRequestWithdrawnEvent(request.Id, request.Key,
            Guid.NewGuid(), StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted, Guid.NewGuid(), StatusCategory.Removed, Actor, Now),
        nameof(IntakeRequestRetitledEvent) => new IntakeRequestRetitledEvent(request.Id, request.Key, "Before", "After", Actor, Now),
        nameof(IntakeRequestDetailsUpdatedEvent) => new IntakeRequestDetailsUpdatedEvent(request.Id, request.Key,
            "After", null, "Before", null, Actor, Now),
        nameof(IntakeRequestNeedByChangedEvent) => new IntakeRequestNeedByChangedEvent(request.Id, request.Key, null, null, Actor, Now),
        nameof(IntakeRequestAssigneeChangedEvent) => new IntakeRequestAssigneeChangedEvent(request.Id, request.Key, null, Guid.NewGuid(), Actor, Now),
        nameof(IntakeRequestReroutedEvent) => new IntakeRequestReroutedEvent(request.Id, request.Key, Guid.NewGuid(), Guid.NewGuid(),
            IntakeDeskVisibility.Open, IntakeDeskVisibility.Open, Actor, Now),
        nameof(IntakeRequestParticipantAddedEvent) => new IntakeRequestParticipantAddedEvent(request.Id, request.Key, Guid.NewGuid(), [], Actor, Now),
        nameof(IntakeRequestParticipantRemovedEvent) => new IntakeRequestParticipantRemovedEvent(request.Id, request.Key, Guid.NewGuid(), [], Actor, Now),
        nameof(IntakeRequestChannelChangedEventV2) => new IntakeRequestChannelChangedEventV2(request.Id, request.Key, null, Guid.NewGuid(), Actor, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
    };

    private Task HandleAny(DomainEvent @event)
    {
        var handler = CreateHandler();
        var ct = TestContext.Current.CancellationToken;
        return @event switch
        {
            IntakeRequestStatusChangedEvent e => handler.Handle(e, ct),
            IntakeRequestWithdrawnEvent e => handler.Handle(e, ct),
            IntakeRequestRetitledEvent e => handler.Handle(e, ct),
            IntakeRequestDetailsUpdatedEvent e => handler.Handle(e, ct),
            IntakeRequestNeedByChangedEvent e => handler.Handle(e, ct),
            IntakeRequestAssigneeChangedEvent e => handler.Handle(e, ct),
            IntakeRequestReroutedEvent e => handler.Handle(e, ct),
            IntakeRequestParticipantAddedEvent e => handler.Handle(e, ct),
            IntakeRequestParticipantRemovedEvent e => handler.Handle(e, ct),
            IntakeRequestChannelChangedEventV2 e => handler.Handle(e, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(@event), @event.GetType().Name, null),
        };
    }
}
