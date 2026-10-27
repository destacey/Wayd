using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));

    private static IntakeRequestEditor ADeskAgent() => new(Guid.NewGuid(), IsDeskAgent: true, EventActor.System);

    private static IntakeRequestEditor SomeoneOffTheDesk() => new(Guid.NewGuid(), IsDeskAgent: false, EventActor.System);

    /// <summary>The status the request already holds, as a move to a desk on the same workflow keeps it.</summary>
    private static StatusRef StayingPut(IntakeRequest request) =>
        new(request.StatusWorkflowId, request.StatusId, request.StatusName, request.StatusCategory, request.StatusAliasValue);

    private static IntakeRequestNeedBy ANeedBy(string reference = "PCI DSS 4.0") =>
        IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, reference, null, null).Value;

    #region Create

    [Fact]
    public void Create_WhenValid_IsAProposedRequestOnTheDesk()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        var channel = new IntakeChannelFaker().Generate();
        var requesterId = Guid.NewGuid();
        var submittedById = Guid.NewGuid();
        var needBy = ANeedBy();
        var origin = IntakeRequestOrigin.Create("ServicePortal", "REQ-1", null).Value;

        // Act
        var result = IntakeRequest.Create(" Export invoices ", " Finance needs a CSV export ", " Saves a day a month ",
            requesterId, submittedById, desk, channel, needBy, origin, IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sut = result.Value;
        sut.Title.Should().Be("Export invoices");
        sut.Description.Should().Be("Finance needs a CSV export");
        sut.ExpectedBenefits.Should().Be("Saves a day a month");
        sut.RequesterId.Should().Be(requesterId);
        sut.SubmittedById.Should().Be(submittedById);
        sut.DeskId.Should().Be(desk.Id);
        sut.ChannelId.Should().Be(channel.Id);
        sut.NeedBy.Should().Be(needBy);
        sut.Origin.Should().Be(origin);
        sut.AssigneeId.Should().BeNull();
        sut.Participants.Should().BeEmpty();
        sut.StatusCategory.Should().Be(StatusCategory.Proposed);
    }

    [Fact]
    public void Create_OnAClosedDesk_Fails()
    {
        // Arrange
        var desk = new IntakeDeskFaker().WithIsOpen(false).Generate();

        // Act
        var result = IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk,
            new IntakeChannelFaker().Generate(), null, null, IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This desk is not accepting new requests.");
    }

    [Fact]
    public void Create_WithAnArchivedChannel_Fails()
    {
        // Act
        var result = IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), new IntakeChannelFaker().AsArchived().Generate(), null, null,
            IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An archived channel cannot be given to a request.");
    }

    [Fact]
    public void Create_WithoutAChannel_HasNone()
    {
        // Act
        var result = IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), null, null, null, IntakeStatusRefFactory.Submitted(), EventActor.System,
            _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelId.Should().BeNull();
    }

    [Fact]
    public void Create_WithAChannelTheDeskDoesNotOffer_Succeeds()
    {
        // Arrange — an upstream system or an import records where the ask came from
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);
        var channel = new IntakeChannelFaker().Generate();

        // Act
        var result = IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk, channel,
            null, IntakeRequestOrigin.Create("ServicePortal", "REQ-7", null).Value, IntakeStatusRefFactory.Submitted(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public void Submit_WithAChannelTheDeskOffers_IsCreated()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        var channel = new IntakeChannelFaker().Generate();

        // Act
        var result = IntakeRequest.Submit("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk, [channel],
            channel, null, IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelId.Should().Be(channel.Id);
        result.Value.Origin.Should().BeNull();
    }

    [Fact]
    public void Submit_WithAChannelTheDeskDoesNotOffer_Fails()
    {
        // Arrange
        var offered = new IntakeChannelFaker().Generate();
        var other = new IntakeChannelFaker().Generate();
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [offered.Id], EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = IntakeRequest.Submit("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk,
            [offered, other], other, null, IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelNotOfferedError);
    }

    [Fact]
    public void Submit_WithoutAChannelToADeskOfferingSome_Fails()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();

        // Act
        var result = IntakeRequest.Submit("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk,
            [new IntakeChannelFaker().Generate()], null, null, IntakeStatusRefFactory.Submitted(), EventActor.System,
            _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelRequiredError);
    }

    [Fact]
    public void Submit_WithoutAChannelToADeskOfferingNone_HasNone()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = IntakeRequest.Submit("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(), desk,
            [new IntakeChannelFaker().Generate()], null, null, IntakeStatusRefFactory.Submitted(), EventActor.System,
            _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ChannelId.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutARequester_Throws()
    {
        // Act
        Action act = () => IntakeRequest.Create("Title", "Description", null, Guid.Empty, Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), new IntakeChannelFaker().Generate(), null, null,
            IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RaisesCreatedEventOnlyOnceKeyed_CarryingTheSummaryAsSubmitted()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        var channel = new IntakeChannelFaker().Generate();
        var requesterId = Guid.NewGuid();
        var needBy = ANeedBy();
        var sut = IntakeRequest.Create("Export invoices", "Description", null, requesterId, Guid.NewGuid(), desk,
            channel, needBy, null, IntakeStatusRefFactory.Submitted(), EventActor.System, _dateTimeProvider.Now).Value;

        // Act — retitled and given another channel before the first save, as an import could
        var other = new IntakeChannelFaker().Generate();
        sut.Retitle("Renamed", IntakeRequestEditor.System, _dateTimeProvider.Now);
        sut.ChangeChannel(other, desk, [channel, other], IntakeRequestEditor.System, _dateTimeProvider.Now);
        sut.DomainEvents.Should().BeEmpty();
        foreach (var action in sut.PostPersistenceActions.ToList())
            action();

        // Assert
        var created = sut.DomainEvents.OfType<IntakeRequestCreatedEventV2>().Should().ContainSingle().Subject;
        created.Title.Should().Be("Export invoices");
        created.DeskId.Should().Be(desk.Id);
        created.ChannelId.Should().Be(channel.Id);
        created.RequesterId.Should().Be(requesterId);
        created.NeedBy.Should().Be(needBy.ToDetails());
        created.StatusId.Should().Be(sut.StatusId);
        created.StatusCategory.Should().Be(StatusCategory.Proposed);
        created.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
    }

    [Fact]
    public void Create_EntersTheSubmittedStatus_RecordingItAsTheFirstTransition()
    {
        // Arrange
        var submitted = IntakeStatusRefFactory.Submitted();

        // Act
        var sut = IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), new IntakeChannelFaker().Generate(), null, null, submitted,
            EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        sut.StatusId.Should().Be(submitted.StatusId);
        sut.StatusWorkflowId.Should().Be(submitted.WorkflowId);
        sut.StatusName.Should().Be(submitted.Name);
        sut.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        sut.StatusOwnerType.Should().Be(DemandManagementWorkflowOwners.IntakeRequest.Key);
        var transition = sut.StatusTransitions.Should().ContainSingle().Subject;
        transition.FromStatusId.Should().BeNull();
        transition.ToStatusId.Should().Be(submitted.StatusId);
        sut.HasLeftItsInitialStatus.Should().BeFalse();
    }

    [Fact]
    public void Create_InAStatusOtherThanSubmitted_Throws()
    {
        // Arrange — the resolver is asked for Submitted; anything else is a caller's bug
        var inReview = IntakeStatusRefFactory.For(StatusCategory.Proposed);

        // Act
        Action act = () => IntakeRequest.Create("Title", "Description", null, Guid.NewGuid(), Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), new IntakeChannelFaker().Generate(), null, null, inReview,
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Create

    #region Edit lock

    [Fact]
    public void CanEdit_WhileProposed_AllowsTheRequesterOffTheDesk()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).Generate();

        // Act
        var result = sut.CanEdit(new IntakeRequestEditor(requesterId, IsDeskAgent: false, EventActor.System));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_WhileProposed_AllowsADeskAgent()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.CanEdit(ADeskAgent());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_WhileProposed_RefusesSomeoneElseOffTheDesk()
    {
        // Arrange — the submitter is not the requester, and only the requester edits without working the desk
        var submittedById = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithSubmittedById(submittedById).Generate();

        // Act
        var result = sut.CanEdit(new IntakeRequestEditor(submittedById, IsDeskAgent: false, EventActor.System));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Retitle_ByAnEditorTheLockRefuses_FailsAndChangesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var title = sut.Title;

        // Act
        var result = sut.Retitle("New title", SomeoneOffTheDesk(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester or an agent on the request's desk may edit this request.");
        sut.Title.Should().Be(title);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CanEdit_WhileActive_RefusesTheRequesterOffTheDesk()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).InStatus(StatusCategory.Active, IntakeRequestStatusAlias.Accepted).Generate();

        // Act
        var result = sut.CanEdit(new IntakeRequestEditor(requesterId, IsDeskAgent: false, EventActor.System));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanEdit_WhileActive_AllowsADeskAgent()
    {
        // Arrange
        var sut = new IntakeRequestFaker().InStatus(StatusCategory.Active, IntakeRequestStatusAlias.Accepted).Generate();

        // Act
        var result = sut.CanEdit(ADeskAgent());

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusCategory.Done)]
    [InlineData(StatusCategory.Removed)]
    public void CanEdit_WhileClosed_RefusesEveryoneIncludingTheDesksAgents(StatusCategory category)
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).InStatus(category).Generate();

        // Act
        var byAgent = sut.CanEdit(ADeskAgent());
        var byRequester = sut.CanEdit(new IntakeRequestEditor(requesterId, IsDeskAgent: false, EventActor.System));

        // Assert
        byAgent.Should().BeFalse();
        byRequester.Should().BeFalse();
    }

    [Fact]
    public void Retitle_ByTheRequesterOnceAccepted_FailsNamingTheAgentsOnly()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).InStatus(StatusCategory.Active, IntakeRequestStatusAlias.Accepted).Generate();

        // Act
        var result = sut.Retitle("New title", new IntakeRequestEditor(requesterId, IsDeskAgent: false, EventActor.System), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Once a request is accepted, only agents on its desk may edit it.");
    }

    [Fact]
    public void Retitle_ByADeskAgentWhileClosed_FailsUntilItIsReopened()
    {
        // Arrange
        var sut = new IntakeRequestFaker().InStatus(StatusCategory.Removed).Generate();
        var title = sut.Title;

        // Act
        var result = sut.Retitle("New title", ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A closed request cannot be edited until it is moved back to an open status.");
        sut.Title.Should().Be(title);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, true)]
    [InlineData(StatusCategory.Active, true)]
    [InlineData(StatusCategory.Done, false)]
    [InlineData(StatusCategory.Removed, false)]
    public void IsOpen_FollowsTheStatusCategory(StatusCategory category, bool expected)
    {
        // Arrange
        var sut = new IntakeRequestFaker().InStatus(category).Generate();

        // Act
        var isOpen = sut.IsOpen;

        // Assert
        isOpen.Should().Be(expected);
    }

    #endregion Edit lock

    #region Retitle

    [Fact]
    public void Retitle_WhenChanged_RaisesRetitledWithBothEnds()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var fromTitle = sut.Title;

        // Act
        var result = sut.Retitle(" New title ", ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Title.Should().Be("New title");
        var retitled = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestRetitledEvent>().Subject;
        retitled.FromTitle.Should().Be(fromTitle);
        retitled.ToTitle.Should().Be("New title");
    }

    [Fact]
    public void Retitle_ToTheSameTitlePadded_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        sut.Retitle($"  {sut.Title}  ", ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Retitle

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_WhenChanged_RaisesDetailsUpdatedWithBothEnds()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var previousDescription = sut.Description;
        var previousExpectedBenefits = sut.ExpectedBenefits;

        // Act
        var result = sut.UpdateDetails("New description", "  ", ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ExpectedBenefits.Should().BeNull();
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestDetailsUpdatedEvent>().Subject;
        updated.Description.Should().Be("New description");
        updated.ExpectedBenefits.Should().BeNull();
        updated.PreviousDescription.Should().Be(previousDescription);
        updated.PreviousExpectedBenefits.Should().Be(previousExpectedBenefits);
    }

    [Fact]
    public void UpdateDetails_WithNothingChanged_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        sut.UpdateDetails(sut.Description + " ", sut.ExpectedBenefits, ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region ChangeNeedBy

    [Fact]
    public void ChangeNeedBy_WhenSet_RaisesNeedByChangedFromNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().WithNeedBy(null).Generate();
        var needBy = ANeedBy();

        // Act
        sut.ChangeNeedBy(needBy, ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        sut.NeedBy.Should().Be(needBy);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestNeedByChangedEvent>().Subject;
        changed.From.Should().BeNull();
        changed.To.Should().Be(needBy.ToDetails());
    }

    [Fact]
    public void ChangeNeedBy_WhenOnlyTheReferenceChanges_RaisesWithBothEnds()
    {
        // Arrange
        var from = ANeedBy("Rule 1");
        var to = ANeedBy("Rule 2");
        var sut = new IntakeRequestFaker().WithNeedBy(from).Generate();

        // Act
        sut.ChangeNeedBy(to, ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestNeedByChangedEvent>().Subject;
        changed.From.Should().Be(from.ToDetails());
        changed.To.Should().Be(to.ToDetails());
    }

    [Fact]
    public void ChangeNeedBy_WhenCleared_ClearsEveryPartAndRaisesToNothing()
    {
        // Arrange
        var from = ANeedBy();
        var sut = new IntakeRequestFaker().WithNeedBy(from).Generate();

        // Act
        sut.ChangeNeedBy(null, ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        sut.NeedBy.Should().BeNull();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestNeedByChangedEvent>().Subject;
        changed.From.Should().Be(from.ToDetails());
        changed.To.Should().BeNull();
    }

    [Fact]
    public void ChangeNeedBy_ToAnEqualValue_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().WithNeedBy(ANeedBy()).Generate();

        // Act
        sut.ChangeNeedBy(ANeedBy(), ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeNeedBy

    #region ChangeChannel

    [Fact]
    public void ChangeChannel_ToOneTheDeskOffers_RaisesWithBothEnds()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        var fromChannelId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).WithChannelId(fromChannelId).Generate();
        var channel = new IntakeChannelFaker().Generate();

        // Act
        var result = sut.ChangeChannel(channel, desk, [channel], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ChannelId.Should().Be(channel.Id);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestChannelChangedEventV2>().Subject;
        changed.FromChannelId.Should().Be(fromChannelId);
        changed.ToChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public void ChangeChannel_ToAnArchivedChannel_Fails()
    {
        // Arrange
        var desk = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).Generate();
        var archived = new IntakeChannelFaker().AsArchived().Generate();

        // Act
        var result = sut.ChangeChannel(archived, desk, [archived], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelNotOfferedError);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeChannel_ToOneTheDeskDoesNotOffer_Fails()
    {
        // Arrange
        var offered = new IntakeChannelFaker().Generate();
        var other = new IntakeChannelFaker().Generate();
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [offered.Id], EventActor.System, _dateTimeProvider.Now);
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).WithChannelId(offered.Id).Generate();

        // Act
        var result = sut.ChangeChannel(other, desk, [offered, other], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelNotOfferedError);
        sut.ChannelId.Should().Be(offered.Id);
    }

    [Fact]
    public void ChangeChannel_ToItsCurrentChannel_RaisesNothingEvenIfTheDeskNoLongerOffersIt()
    {
        // Arrange — a request keeps its channel after the channel is archived or the request is moved
        var channel = new IntakeChannelFaker().AsArchived().Generate();
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).WithChannelId(channel.Id).Generate();

        // Act
        var result = sut.ChangeChannel(channel, desk, [channel], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeChannel_ClearingOnADeskOfferingChannels_Fails()
    {
        // Arrange
        var channel = new IntakeChannelFaker().Generate();
        var desk = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).WithChannelId(channel.Id).Generate();

        // Act
        var result = sut.ChangeChannel(null, desk, [channel], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelRequiredError);
        sut.ChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public void ChangeChannel_ClearingOnADeskOfferingNone_RaisesWithBothEnds()
    {
        // Arrange
        var channel = new IntakeChannelFaker().Generate();
        var desk = new IntakeDeskFaker().Generate();
        desk.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);
        var sut = new IntakeRequestFaker().WithDeskId(desk.Id).WithChannelId(channel.Id).Generate();

        // Act
        var result = sut.ChangeChannel(null, desk, [channel], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ChannelId.Should().BeNull();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestChannelChangedEventV2>().Subject;
        changed.FromChannelId.Should().Be(channel.Id);
        changed.ToChannelId.Should().BeNull();
    }

    [Fact]
    public void ChangeChannel_ForAnotherDesk_Throws()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var channel = new IntakeChannelFaker().Generate();

        // Act
        Action act = () => sut.ChangeChannel(channel, new IntakeDeskFaker().Generate(), [channel], ADeskAgent(), _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion ChangeChannel

    #region ChangeAssignee

    [Fact]
    public void ChangeAssignee_WhenReassigned_RaisesWithBothEnds()
    {
        // Arrange
        var fromAssigneeId = Guid.NewGuid();
        var toAssigneeId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithAssigneeId(fromAssigneeId).Generate();

        // Act
        sut.ChangeAssignee(toAssigneeId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.AssigneeId.Should().Be(toAssigneeId);
        var assigned = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestAssigneeChangedEvent>().Subject;
        assigned.FromAssigneeId.Should().Be(fromAssigneeId);
        assigned.ToAssigneeId.Should().Be(toAssigneeId);
    }

    [Fact]
    public void ChangeAssignee_WhenUnassignedAlready_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().WithAssigneeId(null).Generate();

        // Act
        sut.ChangeAssignee(null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeAssignee

    #region Participants

    [Fact]
    public void AddParticipant_ForSomeoneNew_AddsThemAndRaisesTheResultingSet()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var addedById = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithParticipants(existingId).Generate();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var result = sut.AddParticipant(newId, addedById, EventActor.System, timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Participants.Should().ContainSingle(p => p.EmployeeId == newId && p.AddedById == addedById && p.AddedOn == timestamp);
        var added = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestParticipantAddedEvent>().Subject;
        added.EmployeeId.Should().Be(newId);
        added.ParticipantIds.Should().BeEquivalentTo([existingId, newId]);
    }

    [Fact]
    public void AddParticipant_ForTheRequesterOrSubmitter_Fails()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var submittedById = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).WithSubmittedById(submittedById).Generate();

        // Act
        var forRequester = sut.AddParticipant(requesterId, null, EventActor.System, _dateTimeProvider.Now);
        var forSubmitter = sut.AddParticipant(submittedById, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        forRequester.IsFailure.Should().BeTrue();
        forSubmitter.IsFailure.Should().BeTrue();
        sut.Participants.Should().BeEmpty();
    }

    [Fact]
    public void AddParticipant_ForAnExistingParticipant_Fails()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithParticipants(employeeId).Generate();

        // Act
        var result = sut.AddParticipant(employeeId, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This employee is already a participant.");
    }

    [Fact]
    public void RemoveParticipant_ForAParticipant_RemovesThemAndRaisesTheResultingSet()
    {
        // Arrange
        var keptId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithParticipants(keptId, removedId).Generate();

        // Act
        var result = sut.RemoveParticipant(removedId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Participants.Select(p => p.EmployeeId).Should().Equal(keptId);
        var removed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestParticipantRemovedEvent>().Subject;
        removed.EmployeeId.Should().Be(removedId);
        removed.ParticipantIds.Should().Equal(keptId);
    }

    [Fact]
    public void RemoveParticipant_ForSomeoneWhoIsNotOne_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.RemoveParticipant(Guid.NewGuid(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void IsOwnPerson_CoversTheRequesterTheSubmitterAndParticipantsOnly()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var submittedById = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).WithSubmittedById(submittedById)
            .WithParticipants(participantId).Generate();

        // Act & Assert
        sut.IsOwnPerson(requesterId).Should().BeTrue();
        sut.IsOwnPerson(submittedById).Should().BeTrue();
        sut.IsOwnPerson(participantId).Should().BeTrue();
        sut.IsOwnPerson(Guid.NewGuid()).Should().BeFalse();
    }

    #endregion Participants

    #region Delete

    [Fact]
    public void Delete_RaisesDeletedWithItsTitleAndDesk()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.Delete(hasDuplicates: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestDeletedEvent>().Subject;
        deleted.Title.Should().Be(sut.Title);
        deleted.DeskId.Should().Be(sut.DeskId);
    }

    [Fact]
    public void Delete_OnceItHasLeftItsInitialStatus_FailsAndRaisesNothing()
    {
        // Arrange — moved on and back again: the status is Submitted, but the history is not just Submitted
        var sut = new IntakeRequestFaker().WithStatusTransitionCount(3).Generate();

        // Act
        var result = sut.Delete(hasDuplicates: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.PartOfARecordError);
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Delete

    #region Reroute

    private static (IntakeDesk From, IntakeDesk To, IntakeRequest Request) RerouteScenario(
        IntakeDeskVisibility fromVisibility, IntakeDeskVisibility toVisibility, bool toIsOpen = true)
    {
        var from = new IntakeDeskFaker().WithVisibility(fromVisibility).Generate();
        var to = new IntakeDeskFaker().WithVisibility(toVisibility).WithIsOpen(toIsOpen).Generate();
        var request = new IntakeRequestFaker().WithDeskId(from.Id).Generate();
        return (from, to, request);
    }

    [Fact]
    public void Reroute_ToAnotherDesk_MovesItAndRaisesWithBothEndsAndTheirVisibility()
    {
        // Arrange
        var (from, to, sut) = RerouteScenario(IntakeDeskVisibility.Open, IntakeDeskVisibility.Private);

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), widerVisibilityConfirmed: false, EventActor.System, _dateTimeProvider.Now);

        // Assert — a move to a more private desk needs no confirmation
        result.IsSuccess.Should().BeTrue();
        sut.DeskId.Should().Be(to.Id);
        var rerouted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestReroutedEvent>().Subject;
        rerouted.FromDeskId.Should().Be(from.Id);
        rerouted.ToDeskId.Should().Be(to.Id);
        rerouted.FromVisibility.Should().Be(IntakeDeskVisibility.Open);
        rerouted.ToVisibility.Should().Be(IntakeDeskVisibility.Private);
    }

    [Fact]
    public void Reroute_ToADeskOfferingNoChannel_KeepsTheChannel()
    {
        // Arrange — where the ask came from does not change with who reviews it
        var (from, to, sut) = RerouteScenario(IntakeDeskVisibility.Open, IntakeDeskVisibility.Open);
        to.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);
        var channelId = sut.ChannelId;

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), widerVisibilityConfirmed: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        channelId.Should().NotBeNull();
        sut.ChannelId.Should().Be(channelId);
        sut.DomainEvents.OfType<IntakeRequestChannelChangedEventV2>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(IntakeDeskVisibility.Private, IntakeDeskVisibility.SummaryOnly)]
    [InlineData(IntakeDeskVisibility.SummaryOnly, IntakeDeskVisibility.Open)]
    public void Reroute_ToALessPrivateDeskUnconfirmed_FailsAndStays(IntakeDeskVisibility fromVisibility, IntakeDeskVisibility toVisibility)
    {
        // Arrange
        var (from, to, sut) = RerouteScenario(fromVisibility, toVisibility);

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), widerVisibilityConfirmed: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.WiderVisibilityNotConfirmedError);
        sut.DeskId.Should().Be(from.Id);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reroute_ToALessPrivateDeskConfirmed_Moves()
    {
        // Arrange
        var (from, to, sut) = RerouteScenario(IntakeDeskVisibility.Private, IntakeDeskVisibility.Open);

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), widerVisibilityConfirmed: true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DeskId.Should().Be(to.Id);
    }

    [Fact]
    public void Reroute_ToADeskClosedToNewRequests_Fails()
    {
        // Arrange
        var (from, to, sut) = RerouteScenario(IntakeDeskVisibility.Open, IntakeDeskVisibility.Open, toIsOpen: false);

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This desk is not accepting new requests.");
        sut.DeskId.Should().Be(from.Id);
    }

    [Fact]
    public void Reroute_ToItsOwnDesk_Fails()
    {
        // Arrange
        var (from, _, sut) = RerouteScenario(IntakeDeskVisibility.Open, IntakeDeskVisibility.Open);

        // Act
        var result = sut.Reroute(from, from, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reroute_WithAnAssigneeNotOnTheDestination_UnassignsThemInTheSameMove()
    {
        // Arrange
        var assigneeId = Guid.NewGuid();
        var from = new IntakeDeskFaker().WithAgents(assigneeId).Generate();
        var to = new IntakeDeskFaker().WithAgents(Guid.NewGuid()).Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).WithAssigneeId(assigneeId).Generate();

        // Act
        sut.Reroute(from, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.AssigneeId.Should().BeNull();
        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(IntakeRequestReroutedEvent), typeof(IntakeRequestAssigneeChangedEvent));
    }

    [Fact]
    public void Reroute_WithAnAssigneeWhoAlsoWorksTheDestination_KeepsThem()
    {
        // Arrange
        var assigneeId = Guid.NewGuid();
        var from = new IntakeDeskFaker().WithAgents(assigneeId).Generate();
        var to = new IntakeDeskFaker().WithAgents(assigneeId).Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).WithAssigneeId(assigneeId).Generate();

        // Act
        sut.Reroute(from, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.AssigneeId.Should().Be(assigneeId);
        sut.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Reroute_FromADeskOtherThanItsOwn_Throws()
    {
        // Arrange — the visibility the event records must be the current desk's
        var (_, to, sut) = RerouteScenario(IntakeDeskVisibility.Open, IntakeDeskVisibility.Open);
        var notItsDesk = new IntakeDeskFaker().Generate();

        // Act
        Action act = () => sut.Reroute(notItsDesk, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(StatusCategory.Done)]
    [InlineData(StatusCategory.Removed)]
    public void Reroute_AClosedRequest_FailsAndStays(StatusCategory category)
    {
        // Arrange
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).InStatus(category).Generate();

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A closed request cannot be moved to another desk.");
        sut.DeskId.Should().Be(from.Id);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reroute_AnAcceptedRequest_Moves()
    {
        // Arrange — accepted is still open
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).InStatus(StatusCategory.Active, IntakeRequestStatusAlias.Accepted).Generate();

        // Act
        var result = sut.Reroute(from, to, StayingPut(sut), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DeskId.Should().Be(to.Id);
    }

    [Fact]
    public void Reroute_ToADeskOnTheSameWorkflowInAnotherStatus_MovesBothAndRaisesTheChange()
    {
        // Arrange
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).Generate();
        var fromStatusId = sut.StatusId;
        var inReview = IntakeStatusRefFactory.For(StatusCategory.Proposed, workflowId: sut.StatusWorkflowId);

        // Act
        var result = sut.Reroute(from, to, inReview, false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DeskId.Should().Be(to.Id);
        sut.StatusId.Should().Be(inReview.StatusId);
        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(IntakeRequestStatusChangedEvent), typeof(IntakeRequestReroutedEvent));
        var changed = sut.DomainEvents.OfType<IntakeRequestStatusChangedEvent>().Single();
        changed.FromStatusId.Should().Be(fromStatusId);
        changed.ToStatusId.Should().Be(inReview.StatusId);
    }

    [Fact]
    public void Reroute_ToADeskOnAnotherWorkflow_SwitchesTheRequestOntoIt()
    {
        // Arrange
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).Generate();
        var fromWorkflowId = sut.StatusWorkflowId;
        var theirSubmitted = IntakeStatusRefFactory.Submitted();

        // Act
        var result = sut.Reroute(from, to, theirSubmitted, false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusWorkflowId.Should().Be(theirSubmitted.WorkflowId);
        sut.StatusId.Should().Be(theirSubmitted.StatusId);
        var switched = sut.DomainEvents.OfType<StatusWorkflowSwitchedEvent>().Should().ContainSingle().Subject;
        switched.FromWorkflowId.Should().Be(fromWorkflowId);
        switched.ToWorkflowId.Should().Be(theirSubmitted.WorkflowId);
        sut.StatusTransitions.Should().ContainSingle();
    }

    [Theory]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.None)]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate)]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled)]
    public void Reroute_ArrivingInAClosedStatus_FailsAndChangesNothing(StatusCategory category, IntakeRequestStatusAlias alias)
    {
        // Arrange
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).Generate();
        var statusId = sut.StatusId;
        var arrival = IntakeStatusRefFactory.For(category, alias, sut.StatusWorkflowId);

        // Act
        var result = sut.Reroute(from, to, arrival, false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ArrivalStatusNotOpenError);
        sut.DeskId.Should().Be(from.Id);
        sut.StatusId.Should().Be(statusId);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, true)]
    [InlineData(StatusCategory.Active, true)]
    [InlineData(StatusCategory.Done, false)]
    [InlineData(StatusCategory.Removed, false)]
    public void CanArriveIn_AllowsOnlyAnOpenCategory(StatusCategory category, bool expected)
    {
        // Act — Withdrawn and Duplicate are tied to Removed, so they are refused with it
        var canArrive = IntakeRequest.CanArriveIn(category);

        // Assert
        canArrive.Should().Be(expected);
    }

    [Fact]
    public void Reroute_OntoAnotherWorkflow_ClearsTheStatusReason()
    {
        // Arrange
        var from = new IntakeDeskFaker().Generate();
        var to = new IntakeDeskFaker().Generate();
        var sut = new IntakeRequestFaker().WithDeskId(from.Id).InStatus(StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester)
            .WithStatusReason("Which quarter?").Generate();

        // Act
        var result = sut.Reroute(from, to, IntakeStatusRefFactory.Submitted(), false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DuplicateOfId.Should().BeNull();
        sut.StatusReason.Should().BeNull();
    }

    #endregion Reroute

    #region ChangeStatus

    [Theory]
    [InlineData(StatusCategory.Proposed, IntakeRequestStatusAlias.None, false)]
    [InlineData(StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester, false)]
    [InlineData(StatusCategory.Active, IntakeRequestStatusAlias.Accepted, false)]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled, true)]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.None, true)]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.None, true)]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate, true)]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn, false)]
    public void NeedsReason_FollowsTheCategory_ExceptForWithdrawn(StatusCategory category, IntakeRequestStatusAlias alias, bool expected)
    {
        // Act — a company's own "Won't do" needs a reason with no meaning set; Withdrawn is the requester's own
        var needsReason = IntakeRequest.NeedsReason(category, alias);

        // Assert
        needsReason.Should().Be(expected);
    }

    private static StatusRef InItsWorkflow(IntakeRequest request, StatusCategory category,
        IntakeRequestStatusAlias alias = IntakeRequestStatusAlias.None) =>
        IntakeStatusRefFactory.For(category, alias, request.StatusWorkflowId);

    [Fact]
    public void ChangeStatus_ToAnotherStage_MovesItAndRaisesWithBothEnds()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var fromStatusId = sut.StatusId;
        var inReview = InItsWorkflow(sut, StatusCategory.Proposed);

        // Act
        var result = sut.ChangeStatus(inReview, null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusId.Should().Be(inReview.StatusId);
        sut.HasLeftItsInitialStatus.Should().BeTrue();
        sut.StatusTransitions.Should().ContainSingle().Which.FromStatusId.Should().Be(fromStatusId);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestStatusChangedEvent>().Subject;
        changed.FromStatusId.Should().Be(fromStatusId);
        changed.FromCategory.Should().Be(StatusCategory.Proposed);
        changed.FromAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        changed.ToStatusId.Should().Be(inReview.StatusId);
        changed.ToAlias.Should().Be(IntakeRequestStatusAlias.None);
        changed.FromDuplicateOfId.Should().BeNull();
        changed.ToDuplicateOfId.Should().BeNull();
    }

    [Theory]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.None)]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled)]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.None)]
    public void ChangeStatus_ToAStatusNeedingAReason_WithoutOne_FailsAndChangesNothing(StatusCategory category, IntakeRequestStatusAlias alias)
    {
        // Arrange — a company's own closing or finishing status needs one as much as Fulfilled does
        var sut = new IntakeRequestFaker().Generate();
        var statusId = sut.StatusId;

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, category, alias), "  ", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().EndWith("needs a reason the requester can read.");
        sut.StatusId.Should().Be(statusId);
        sut.StatusTransitions.Should().BeEmpty();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToAClosingStatusWithAReason_KeepsItOnTheRequestAndTheTransition()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed),
            " Budget went to the platform rebuild ", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusReason.Should().Be("Budget went to the platform rebuild");
        sut.StatusTransitions.Should().ContainSingle().Which.Reason.Should().Be("Budget went to the platform rebuild");
        sut.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ChangeStatus_ToWithdrawn_FailsBecauseOnlyTheRequesterWithdraws()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn),
            "Asked to", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester can withdraw a request.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToDuplicate_RecordsTheSurvivingRequest()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var survivor = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
            "Same ask as the finance export", survivor, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DuplicateOfId.Should().Be(survivor.Id);
        var changed = sut.DomainEvents.OfType<IntakeRequestStatusChangedEvent>().Single();
        changed.FromDuplicateOfId.Should().BeNull();
        changed.ToDuplicateOfId.Should().Be(survivor.Id);
    }

    [Fact]
    public void ChangeStatus_ToDuplicateWithoutASurvivor_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
            "Seen before", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Name the request this one duplicates.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToADuplicateOfItself_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
            "Seen before", sut, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToADuplicateOfADuplicate_Fails_SoRedirectsNeverChain()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var alreadyADuplicate = new IntakeRequestFaker().InStatus(StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate).Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate),
            "Seen before", alreadyADuplicate, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That request is itself a duplicate. Name the request it gives way to instead.");
    }

    [Fact]
    public void ChangeStatus_NamingASurvivorForAnythingButDuplicate_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Proposed), null, new IntakeRequestFaker().Generate(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a duplicate names a surviving request.");
    }

    [Fact]
    public void ChangeStatus_AwayFromDuplicate_ClearsTheSurvivorAndTheReason()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var survivor = new IntakeRequestFaker().Generate();
        sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Removed, IntakeRequestStatusAlias.Duplicate), "Seen before", survivor,
            EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act — reopened by an agent
        var result = sut.ChangeStatus(InItsWorkflow(sut, StatusCategory.Proposed), null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DuplicateOfId.Should().BeNull();
        sut.StatusReason.Should().BeNull();
        sut.IsOpen.Should().BeTrue();
        var changed = sut.DomainEvents.OfType<IntakeRequestStatusChangedEvent>().Single();
        changed.FromDuplicateOfId.Should().Be(survivor.Id);
        changed.ToDuplicateOfId.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_ToTheStatusItHolds_ChangesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();
        var same = new StatusRef(sut.StatusWorkflowId, sut.StatusId, sut.StatusName, sut.StatusCategory, sut.StatusAliasValue);

        // Act
        var result = sut.ChangeStatus(same, null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusTransitions.Should().BeEmpty();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_ToAStatusOfAnotherWorkflow_Throws()
    {
        // Arrange — moving onto another workflow is a switch, never a plain status change
        var sut = new IntakeRequestFaker().Generate();

        // Act
        Action act = () => sut.ChangeStatus(IntakeStatusRefFactory.For(StatusCategory.Proposed), null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion ChangeStatus

    #region Withdraw

    private static StatusRef WithdrawnIn(IntakeRequest request) =>
        IntakeStatusRefFactory.For(StatusCategory.Removed, IntakeRequestStatusAlias.Withdrawn, request.StatusWorkflowId);

    [Fact]
    public void Withdraw_ByTheRequester_ClosesItAndRaisesWithBothEnds()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).Generate();
        var fromStatusId = sut.StatusId;
        var withdrawn = WithdrawnIn(sut);

        // Act
        var result = sut.Withdraw(requesterId, withdrawn, " Solved it another way ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusAlias.Should().Be(IntakeRequestStatusAlias.Withdrawn);
        sut.IsOpen.Should().BeFalse();
        sut.StatusReason.Should().Be("Solved it another way");
        var withdrawnEvent = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestWithdrawnEvent>().Subject;
        withdrawnEvent.FromStatusId.Should().Be(fromStatusId);
        withdrawnEvent.FromAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        withdrawnEvent.ToStatusId.Should().Be(withdrawn.StatusId);
        withdrawnEvent.ToCategory.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public void Withdraw_ByTheSubmitter_Fails()
    {
        // Arrange — entered on the requester's behalf; it is still their ask
        var submittedById = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithSubmittedById(submittedById).Generate();

        // Act
        var result = sut.Withdraw(submittedById, WithdrawnIn(sut), null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester can withdraw their request.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Withdraw_AClosedRequest_Fails()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).InStatus(StatusCategory.Removed).Generate();

        // Act
        var result = sut.Withdraw(requesterId, WithdrawnIn(sut), null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only an open request can be withdrawn.");
    }

    [Fact]
    public void Withdraw_AnAcceptedRequest_IsAllowed()
    {
        // Arrange — accepted is still open
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).InStatus(StatusCategory.Active, IntakeRequestStatusAlias.Accepted).Generate();

        // Act
        var result = sut.Withdraw(requesterId, WithdrawnIn(sut), null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Withdraw_IntoAStatusThatDoesNotMeanWithdrawn_Throws()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).Generate();

        // Act
        Action act = () => sut.Withdraw(requesterId, InItsWorkflow(sut, StatusCategory.Removed), null,
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Withdraw

    #region Delete with duplicates

    [Fact]
    public void Delete_WhenOtherRequestsAreItsDuplicates_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.Delete(hasDuplicates: true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.PartOfARecordError);
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Delete with duplicates
}
