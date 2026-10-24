using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));

    private static IntakeRequestEditor AnUpdateHolder() => new(Guid.NewGuid(), HoldsUpdate: true, EventActor.System);

    private static IntakeRequestEditor SomeoneWithoutUpdate() => new(Guid.NewGuid(), HoldsUpdate: false, EventActor.System);

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
            requesterId, submittedById, desk, channel, needBy, origin, EventActor.System, _dateTimeProvider.Now);

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
        sut.AssignedReviewerId.Should().BeNull();
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
            new IntakeChannelFaker().Generate(), null, null, EventActor.System, _dateTimeProvider.Now);

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
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An archived channel cannot be given to a request.");
    }

    [Fact]
    public void Create_WithoutARequester_Throws()
    {
        // Act
        Action act = () => IntakeRequest.Create("Title", "Description", null, Guid.Empty, Guid.NewGuid(),
            new IntakeDeskFaker().Generate(), new IntakeChannelFaker().Generate(), null, null,
            EventActor.System, _dateTimeProvider.Now);

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
            channel, needBy, null, EventActor.System, _dateTimeProvider.Now).Value;

        // Act — retitled before the first save, as an import could
        sut.Retitle("Renamed", IntakeRequestEditor.System, _dateTimeProvider.Now);
        sut.DomainEvents.Should().BeEmpty();
        foreach (var action in sut.PostPersistenceActions.ToList())
            action();

        // Assert
        var created = sut.DomainEvents.OfType<IntakeRequestCreatedEvent>().Should().ContainSingle().Subject;
        created.Title.Should().Be("Export invoices");
        created.DeskId.Should().Be(desk.Id);
        created.ChannelId.Should().Be(channel.Id);
        created.RequesterId.Should().Be(requesterId);
        created.NeedBy.Should().Be(needBy.ToDetails());
    }

    #endregion Create

    #region Edit lock

    [Fact]
    public void CanEdit_WhileProposed_AllowsTheRequesterWithoutUpdate()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithRequesterId(requesterId).Generate();

        // Act
        var result = sut.CanEdit(new IntakeRequestEditor(requesterId, HoldsUpdate: false, EventActor.System));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_WhileProposed_AllowsAnUpdateHolder()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.CanEdit(AnUpdateHolder());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEdit_WhileProposed_RefusesSomeoneElseWithoutUpdate()
    {
        // Arrange — the submitter is not the requester, and only the requester edits without Update
        var submittedById = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithSubmittedById(submittedById).Generate();

        // Act
        var result = sut.CanEdit(new IntakeRequestEditor(submittedById, HoldsUpdate: false, EventActor.System));

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
        var result = sut.Retitle("New title", SomeoneWithoutUpdate(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester or someone who can update requests may edit this request.");
        sut.Title.Should().Be(title);
        sut.DomainEvents.Should().BeEmpty();
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
        var result = sut.Retitle(" New title ", AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.Retitle($"  {sut.Title}  ", AnUpdateHolder(), _dateTimeProvider.Now);

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
        var result = sut.UpdateDetails("New description", "  ", AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.UpdateDetails(sut.Description + " ", sut.ExpectedBenefits, AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.ChangeNeedBy(needBy, AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.ChangeNeedBy(to, AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.ChangeNeedBy(null, AnUpdateHolder(), _dateTimeProvider.Now);

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
        sut.ChangeNeedBy(ANeedBy(), AnUpdateHolder(), _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeNeedBy

    #region ChangeChannel

    [Fact]
    public void ChangeChannel_ToAnActiveChannel_RaisesWithBothEnds()
    {
        // Arrange
        var fromChannelId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithChannelId(fromChannelId).Generate();
        var channel = new IntakeChannelFaker().Generate();

        // Act
        var result = sut.ChangeChannel(channel, AnUpdateHolder(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ChannelId.Should().Be(channel.Id);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestChannelChangedEvent>().Subject;
        changed.FromChannelId.Should().Be(fromChannelId);
        changed.ToChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public void ChangeChannel_ToAnArchivedChannel_Fails()
    {
        // Arrange
        var sut = new IntakeRequestFaker().Generate();

        // Act
        var result = sut.ChangeChannel(new IntakeChannelFaker().AsArchived().Generate(), AnUpdateHolder(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeChannel_ToItsCurrentChannel_RaisesNothingEvenIfArchived()
    {
        // Arrange — a request keeps the channel it has after the channel is archived
        var channel = new IntakeChannelFaker().AsArchived().Generate();
        var sut = new IntakeRequestFaker().WithChannelId(channel.Id).Generate();

        // Act
        var result = sut.ChangeChannel(channel, AnUpdateHolder(), _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeChannel

    #region AssignReviewer

    [Fact]
    public void AssignReviewer_WhenReassigned_RaisesWithBothEnds()
    {
        // Arrange
        var fromReviewerId = Guid.NewGuid();
        var toReviewerId = Guid.NewGuid();
        var sut = new IntakeRequestFaker().WithAssignedReviewerId(fromReviewerId).Generate();

        // Act
        sut.AssignReviewer(toReviewerId, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.AssignedReviewerId.Should().Be(toReviewerId);
        var assigned = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestReviewerAssignedEvent>().Subject;
        assigned.FromReviewerId.Should().Be(fromReviewerId);
        assigned.ToReviewerId.Should().Be(toReviewerId);
    }

    [Fact]
    public void AssignReviewer_WhenUnassignedAlready_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestFaker().WithAssignedReviewerId(null).Generate();

        // Act
        sut.AssignReviewer(null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion AssignReviewer

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
        var result = sut.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestDeletedEvent>().Subject;
        deleted.Title.Should().Be(sut.Title);
        deleted.DeskId.Should().Be(sut.DeskId);
    }

    #endregion Delete
}
