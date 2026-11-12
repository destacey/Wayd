using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.EventHandlers;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.EventHandlers;

public sealed class IntakeRequestNotificationEventHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 15, 0);
    private static readonly EventActor Actor = EventActor.User("actor-user", Guid.NewGuid());

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly FakeDiscussionsDbContext _discussions = new();
    private readonly Mock<INotificationWriter> _writer = new();
    private readonly List<(RecordChange Change, IReadOnlyCollection<NotificationRecipient> Involved)> _notified = [];

    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _submitter = Guid.NewGuid();
    private readonly Guid _assignee = Guid.NewGuid();
    private readonly Guid _participant = Guid.NewGuid();

    public IntakeRequestNotificationEventHandlerTests()
    {
        DemandManagementDiscussionChannels.Register();
        _writer.Setup(w => w.ShouldNotify(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.Notify(It.IsAny<RecordChange>(), It.IsAny<IReadOnlyCollection<NotificationRecipient>>(), It.IsAny<CancellationToken>()))
            .Callback((RecordChange change, IReadOnlyCollection<NotificationRecipient> involved, CancellationToken _) => _notified.Add((change, involved)))
            .ReturnsAsync(1);
    }

    private IntakeRequestNotificationEventHandler CreateHandler() => new(_dbContext, _discussions, _writer.Object);

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

    [Theory]
    [InlineData(DiscussionMessageKind.Public, NotificationEvent.MessagePosted)]
    [InlineData(DiscussionMessageKind.InternalNote, NotificationEvent.InternalNotePosted)]
    public async Task Handle_ShouldNotifyAMessageAsItsKind(DiscussionMessageKind kind, NotificationEvent expected)
    {
        // Arrange
        var request = AddRequest();
        var @event = Posted(request.Id, kind);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        var (change, _) = _notified.Should().ContainSingle().Subject;
        change.Should().Be(new RecordChange(DemandManagementFollowOwners.IntakeRequest.Key, request.Id, expected, @event));
    }

    [Theory]
    [InlineData(NotificationEvent.MessagePosted, IntakeRequestReadTier.Substance)]
    [InlineData(NotificationEvent.InternalNotePosted, IntakeRequestReadTier.Internal)]
    public void NotificationTier_OfAMessage_ShouldBeTheTierOfWhatItSays(NotificationEvent notificationEvent, IntakeRequestReadTier expected)
    {
        // Act
        var tier = IntakeRequestNotificationTiers.TierOf(notificationEvent);

        // Assert
        // A note told at substance tier would tell a requester following their own request that a note exists.
        tier.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldTellTheRequestsPeopleAndThoseMentioned_OfAReply()
    {
        // Arrange
        var request = AddRequest();
        var mentioned = Guid.NewGuid();

        // Act
        await CreateHandler().Handle(Posted(request.Id, DiscussionMessageKind.Public, mentioned: [mentioned]), TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_requester, NotificationReason.Requester),
            new(_assignee, NotificationReason.Assigned),
            new(_participant, NotificationReason.Participant),
            new(_submitter, NotificationReason.Participant),
            new(mentioned, NotificationReason.Mentioned),
        });
    }

    [Fact]
    public async Task Handle_ShouldTellOnlyTheAssigneeAndThoseMentioned_OfAnInternalNote()
    {
        // Arrange — never the requester or the participants, who cannot read it
        var request = AddRequest();
        var mentionedAgent = Guid.NewGuid();

        // Act
        await CreateHandler().Handle(Posted(request.Id, DiscussionMessageKind.InternalNote, mentioned: [mentionedAgent]), TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(_assignee, NotificationReason.Assigned),
            new(mentionedAgent, NotificationReason.Mentioned),
        });
    }

    [Fact]
    public async Task Handle_ShouldTellTheThreadsPeople_OfAReplyInIt()
    {
        // Arrange
        var request = AddRequest();
        var starter = Guid.NewGuid();
        var earlierReplier = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var channel = DiscussionChannel.OpenForRecord(DemandManagementDiscussionChannels.IntakeRequest.Key, request.Id, Actor, Now).Value;
        var parent = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, starter, null, "Question", [], Actor, Now).Value;
        var earlier = DiscussionMessage.Reply(channel, parent, earlierReplier, "Answer", [], Actor, Now).Value;
        var otherThread = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, elsewhere, null, "Unrelated", [], Actor, Now).Value;
        _discussions.AddMessages(parent, earlier, otherThread);

        // Act
        await CreateHandler().Handle(Posted(request.Id, DiscussionMessageKind.Public, parentId: parent.Id), TestContext.Current.CancellationToken);

        // Assert
        Involved().Where(r => r.Reason == NotificationReason.Thread).Select(r => r.EmployeeId)
            .Should().BeEquivalentTo([starter, earlierReplier]);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAMessageOnAnotherKindOfRecord()
    {
        // Arrange
        var request = AddRequest();
        var @event = new DiscussionMessagePostedEvent(Guid.NewGuid(), Guid.NewGuid(), "ppm.project", request.Id, null,
            DiscussionMessageKind.Public, Actor.EmployeeId!.Value, [], Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _notified.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyAMessage_WhenTheChangeCannotNotifyAnyone()
    {
        // Arrange
        var request = AddRequest();
        var @event = Posted(request.Id, DiscussionMessageKind.Public);
        _writer.Setup(w => w.ShouldNotify(@event, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _notified.Should().BeEmpty();
    }

    #region Triage

    [Fact]
    public async Task Handle_ShouldTellOnlyTheDesksTriagers_OfARequestCreatedUnassigned()
    {
        // Arrange
        var (desk, triager, otherTriager) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);

        // Act
        await CreateHandler().Handle(Created(request), TestContext.Current.CancellationToken);

        // Assert — the submitter knows what they submitted, so nobody else on the request is told
        var (change, involved) = _notified.Should().ContainSingle().Subject;
        change.Event.Should().Be(NotificationEvent.Created);
        involved.Should().BeEquivalentTo(new NotificationRecipient[]
        {
            new(triager, NotificationReason.Desk),
            new(otherTriager, NotificationReason.Desk),
        });
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyACreation_WhenTheDeskHasNoTriagers()
    {
        // Arrange — the writer would otherwise tell followers of a creation nobody else hears about
        var desk = new IntakeDeskFaker().WithAgents(Guid.NewGuid()).Generate();
        _dbContext.AddDesks(desk);
        var request = AddRequest(unassigned: true, deskId: desk.Id);

        // Act
        await CreateHandler().Handle(Created(request), TestContext.Current.CancellationToken);

        // Assert
        _notified.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotNotifyACreation_WhenTheRequestIsAlreadyAssigned()
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(deskId: desk.Id);

        // Act
        await CreateHandler().Handle(Created(request), TestContext.Current.CancellationToken);

        // Assert
        _notified.Should().BeEmpty();
    }

    [Fact]
    public void NotificationTier_OfACreation_ShouldMatchTheTierOfItsEvent()
    {
        // Act
        var tier = IntakeRequestNotificationTiers.TierOf(NotificationEvent.Created);

        // Assert
        tier.Should().Be(IntakeRequestCreatedEventV2.ReadTier);
    }

    [Fact]
    public async Task Handle_ShouldTellEveryTriagerButNoOtherMember()
    {
        // Arrange
        var (desk, triager, otherTriager) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);

        // Act
        await CreateHandler().Handle(Created(request), TestContext.Current.CancellationToken);

        // Assert — the desk's agent who does not triage is not told
        Involved().Select(r => r.EmployeeId).Should().BeEquivalentTo([triager, otherTriager]);
    }

    [Fact]
    public async Task Handle_ShouldTellTheDesksTheRerouteNamed_WhenDeliveredAfterTheRequestMovedOn()
    {
        // Arrange — rerouted from one desk to another, then on to a third, with the first reroute delivered last
        var (from, fromTriager, _) = AddDeskWithTriager();
        var (to, toTriager, _) = AddDeskWithTriager();
        var (now, nowTriager, _) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: now.Id);
        var @event = new IntakeRequestReroutedEvent(request.Id, request.Key, from.Id, to.Id,
            IntakeDeskVisibility.Open, IntakeDeskVisibility.Open, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        var told = Involved().Where(r => r.Reason == NotificationReason.Desk).Select(r => r.EmployeeId).ToList();
        told.Should().Contain([fromTriager, toTriager]).And.NotContain(nowTriager);
    }

    [Fact]
    public async Task Handle_ShouldTellATriagerWhoTookTheRequest_OnlyAsItsAssignee()
    {
        // Arrange
        var (desk, triager, otherTriager) = AddDeskWithTriager();
        var request = new IntakeRequestFaker().WithDeskId(desk.Id).WithRequesterId(_requester).WithAssigneeId(triager).Generate();
        _dbContext.AddRequests(request);
        var @event = new IntakeRequestAssigneeChangedEvent(request.Id, request.Key, null, triager, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert — muting Assigned must silence it for them, so it does not also arrive as Desk
        var involved = Involved();
        involved.Should().NotContain(new NotificationRecipient(triager, NotificationReason.Desk));
        involved.Should().Contain(new NotificationRecipient(triager, NotificationReason.Assigned));
        involved.Should().Contain(new NotificationRecipient(otherTriager, NotificationReason.Desk));
    }

    [Fact]
    public async Task Handle_ShouldTellTheTriagersOfBothDesks_OfAnUnassignedReroute()
    {
        // Arrange
        var (from, fromTriager, fromOther) = AddDeskWithTriager();
        var (to, toTriager, toOther) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: to.Id);
        var @event = new IntakeRequestReroutedEvent(request.Id, request.Key, from.Id, to.Id,
            IntakeDeskVisibility.Open, IntakeDeskVisibility.Open, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Where(r => r.Reason == NotificationReason.Desk).Select(r => r.EmployeeId)
            .Should().BeEquivalentTo([fromTriager, fromOther, toTriager, toOther]);
    }

    [Fact]
    public async Task Handle_ShouldTellTheTriagers_WhenTheRequestIsTaken()
    {
        // Arrange
        var (desk, triager, _) = AddDeskWithTriager();
        var request = AddRequest(deskId: desk.Id);
        var @event = new IntakeRequestAssigneeChangedEvent(request.Id, request.Key, null, _assignee, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().Contain(new NotificationRecipient(triager, NotificationReason.Desk));
    }

    [Fact]
    public async Task Handle_ShouldNotTellTheTriagers_OfAHandOverBetweenAssignees()
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(deskId: desk.Id);
        var @event = new IntakeRequestAssigneeChangedEvent(request.Id, request.Key, Guid.NewGuid(), _assignee, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().NotContain(r => r.Reason == NotificationReason.Desk);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Handle_ShouldTellTheTriagersOfAnUnassignedEdit_OnlyWhenTheRequesterMadeIt(bool byRequester, bool told)
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);
        var actor = byRequester ? EventActor.User("requester-user", _requester) : Actor;
        var @event = new IntakeRequestRetitledEvent(request.Id, request.Key, "Before", "After", actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Any(r => r.Reason == NotificationReason.Desk).Should().Be(told);
    }

    [Fact]
    public async Task Handle_ShouldNotTellTheTriagers_OfARequesterEditOnceAssigned()
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(deskId: desk.Id);
        var @event = new IntakeRequestDetailsUpdatedEvent(request.Id, request.Key, "After", null, "Before", null,
            EventActor.User("requester-user", _requester), Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().NotContain(r => r.Reason == NotificationReason.Desk);
    }

    [Theory]
    [InlineData(DiscussionMessageKind.Public, true, true)]
    [InlineData(DiscussionMessageKind.Public, false, false)]
    [InlineData(DiscussionMessageKind.InternalNote, false, false)]
    public async Task Handle_ShouldTellTheTriagersOfAMessage_OnlyWhenTheRequesterRepliedUnassigned(
        DiscussionMessageKind kind, bool byRequester, bool told)
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);
        var author = byRequester ? _requester : Actor.EmployeeId!.Value;
        var @event = new DiscussionMessagePostedEvent(Guid.NewGuid(), Guid.NewGuid(), DemandManagementDiscussionChannels.IntakeRequest.Key,
            request.Id, null, kind, author, [], Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _notified.SelectMany(n => n.Involved).Any(r => r.Reason == NotificationReason.Desk).Should().Be(told);
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, StatusCategory.Done, true)]
    [InlineData(StatusCategory.Active, StatusCategory.Removed, true)]
    [InlineData(StatusCategory.Proposed, StatusCategory.Active, false)]
    [InlineData(StatusCategory.Done, StatusCategory.Removed, false)]
    public async Task Handle_ShouldTellTheTriagersOfAnUnassignedStatusChange_OnlyWhenItCloses(
        StatusCategory from, StatusCategory to, bool told)
    {
        // Arrange
        var (desk, _, _) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);
        var @event = new IntakeRequestStatusChangedEvent(request.Id, request.Key,
            Guid.NewGuid(), from, IntakeRequestStatusAlias.Submitted, Guid.NewGuid(), to, IntakeRequestStatusAlias.Accepted,
            null, null, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Any(r => r.Reason == NotificationReason.Desk).Should().Be(told);
    }

    [Fact]
    public async Task Handle_ShouldTellTheTriagers_OfAnUnassignedWithdrawal()
    {
        // Arrange
        var (desk, triager, _) = AddDeskWithTriager();
        var request = AddRequest(unassigned: true, deskId: desk.Id);
        var @event = new IntakeRequestWithdrawnEvent(request.Id, request.Key,
            Guid.NewGuid(), StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted, Guid.NewGuid(), StatusCategory.Removed, Actor, Now);

        // Act
        await CreateHandler().Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        Involved().Should().Contain(new NotificationRecipient(triager, NotificationReason.Desk));
    }

    /// <summary>A desk with an agent and an admin who triage, and an agent who does not.</summary>
    private (IntakeDesk Desk, Guid Triager, Guid OtherTriager) AddDeskWithTriager()
    {
        var triager = Guid.NewGuid();
        var otherTriager = Guid.NewGuid();
        var desk = new IntakeDeskFaker()
            .WithMembersAndTriagers(
                [new(triager, IntakeDeskRole.Agent), new(otherTriager, IntakeDeskRole.Admin), new(Guid.NewGuid(), IntakeDeskRole.Agent)],
                triagers: [triager, otherTriager])
            .Generate();
        _dbContext.AddDesks(desk);
        return (desk, triager, otherTriager);
    }

    private static IntakeRequestCreatedEventV2 Created(IntakeRequest request) =>
        new(request.Id, request.Key, "New request", request.DeskId, null, request.RequesterId, null,
            Guid.NewGuid(), StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted, Actor, Now);

    #endregion Triage

    private static DiscussionMessagePostedEvent Posted(Guid requestId, DiscussionMessageKind kind, Guid? parentId = null,
        IReadOnlyList<Guid>? mentioned = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), DemandManagementDiscussionChannels.IntakeRequest.Key, requestId, parentId, kind,
            Actor.EmployeeId!.Value, mentioned ?? [], Actor, Now);

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
