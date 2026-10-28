using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeDeskTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));

    #region Create

    [Fact]
    public void Create_WhenValid_SetsEverySetting()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create(" IT ", " Laptops, access and internal tools ", IntakeDeskVisibility.SummaryOnly,
            isRequesterSelectable: true, isOpen: false, requesterMayAddParticipants: false,
            [new(adminId, IntakeDeskRole.Admin), new(agentId, IntakeDeskRole.Agent)],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("IT");
        sut.Description.Should().Be("Laptops, access and internal tools");
        sut.Visibility.Should().Be(IntakeDeskVisibility.SummaryOnly);
        sut.IsRequesterSelectable.Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
        sut.RequesterMayAddParticipants.Should().BeFalse();
        sut.Members.Should().BeEquivalentTo(
        [
            new { DeskId = sut.Id, EmployeeId = adminId, Role = IntakeDeskRole.Admin },
            new { DeskId = sut.Id, EmployeeId = agentId, Role = IntakeDeskRole.Agent },
        ]);
    }

    [Fact]
    public void Create_WithARepeatedMember_NamesThemOnce()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(agentId, IntakeDeskRole.Agent), new(agentId, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Members.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithAnEmployeeInTwoRoles_Throws()
    {
        // Arrange
        var employeeId = Guid.NewGuid();

        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(employeeId, IntakeDeskRole.Admin), new(employeeId, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnEmptyMemberId_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(Guid.Empty, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnUndefinedRole_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Open, true, true, true,
            [new(Guid.NewGuid(), (IntakeDeskRole)99)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("  ", "Internal tools")]
    [InlineData("IT", "  ")]
    public void Create_WithABlankNameOrDescription_Throws(string name, string description)
    {
        // Act
        Action act = () => IntakeDesk.Create(name, description, IntakeDeskVisibility.Open, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithAnUndefinedVisibility_Throws()
    {
        // Act
        Action act = () => IntakeDesk.Create("IT", "Internal tools", (IntakeDeskVisibility)99, true, true, true,
            [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RaisesCreatedEventOnlyOnceKeyed_WithTheDeskAsCreated()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var timestamp = _dateTimeProvider.Now;

        // Act
        var sut = IntakeDesk.Create("IT", "Internal tools", IntakeDeskVisibility.Private, false, true, false,
            [new(adminId, IntakeDeskRole.Admin)], EventActor.System, timestamp);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.Should().ContainSingle().Which();

        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("IT");
        created.Description.Should().Be("Internal tools");
        created.Visibility.Should().Be(IntakeDeskVisibility.Private);
        created.IsRequesterSelectable.Should().BeFalse();
        created.IsOpen.Should().BeTrue();
        created.RequesterMayAddParticipants.Should().BeFalse();
        created.Members.Should().Equal(new IntakeDeskMemberDetails(adminId, IntakeDeskRole.Admin));
        created.Timestamp.Should().Be(timestamp);
    }

    #endregion Create

    #region RoleOf

    [Theory]
    [InlineData(IntakeDeskRole.Admin)]
    [InlineData(IntakeDeskRole.Agent)]
    public void RoleOf_ForAMember_IsTheirRole(IntakeDeskRole role)
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var sut = new IntakeDeskFaker()
            .WithMembers(new(Guid.NewGuid(), IntakeDeskRole.Agent), new(employeeId, role))
            .Generate();

        // Act
        var result = sut.RoleOf(employeeId);

        // Assert
        result.Should().Be(role);
    }

    [Fact]
    public void RoleOf_ForSomeoneNotNamed_IsNull()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithAgents(Guid.NewGuid()).Generate();

        // Act
        var result = sut.RoleOf(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion RoleOf

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_WhenChanged_RaisesWithBothEnds()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithName("IT").Generate();
        var previousDescription = sut.Description;

        // Act
        var result = sut.UpdateDetails(" IT services ", " Laptops and access ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("IT services");
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("IT services");
        updated.Description.Should().Be("Laptops and access");
        updated.PreviousName.Should().Be("IT");
        updated.PreviousDescription.Should().Be(previousDescription);
    }

    [Fact]
    public void UpdateDetails_DifferingOnlyByWhitespace_RaisesNothing()
    {
        // Arrange — the setters trim, so the comparison is after assignment
        var sut = new IntakeDeskFaker().Generate();

        // Act
        sut.UpdateDetails($" {sut.Name} ", $"{sut.Description}  ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region ChangeVisibility

    [Fact]
    public void ChangeVisibility_WhenChanged_RaisesWithBothEnds()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.Private).Generate();

        // Act
        sut.ChangeVisibility(IntakeDeskVisibility.Open, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Visibility.Should().Be(IntakeDeskVisibility.Open);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskVisibilityChangedEvent>().Subject;
        changed.FromVisibility.Should().Be(IntakeDeskVisibility.Private);
        changed.ToVisibility.Should().Be(IntakeDeskVisibility.Open);
    }

    [Fact]
    public void ChangeVisibility_ToTheSameValue_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithVisibility(IntakeDeskVisibility.SummaryOnly).Generate();

        // Act
        sut.ChangeVisibility(IntakeDeskVisibility.SummaryOnly, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeVisibility

    #region Open and Close

    [Fact]
    public void Close_AnOpenDesk_ClosesItAndRaises()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsOpen(true).Generate();

        // Act
        sut.Close(EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.IsOpen.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskClosedEvent>();
    }

    [Fact]
    public void Open_AClosedDesk_OpensItAndRaises()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsOpen(false).Generate();

        // Act
        sut.Open(EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.IsOpen.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskOpenedEvent>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenOrClose_AlreadyThere_RaisesNothing(bool isOpen)
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsOpen(isOpen).Generate();

        // Act
        var result = isOpen ? sut.Open(EventActor.System, _dateTimeProvider.Now) : sut.Close(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Open and Close

    #region ChangeRequesterSettings

    [Fact]
    public void ChangeRequesterSettings_WhenChanged_RaisesWithBothEnds()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsRequesterSelectable(true).WithRequesterMayAddParticipants(true).Generate();

        // Act
        sut.ChangeRequesterSettings(false, true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.IsRequesterSelectable.Should().BeFalse();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskRequesterSettingsChangedEvent>().Subject;
        changed.IsRequesterSelectable.Should().BeFalse();
        changed.PreviousIsRequesterSelectable.Should().BeTrue();
        changed.RequesterMayAddParticipants.Should().BeTrue();
        changed.PreviousRequesterMayAddParticipants.Should().BeTrue();
    }

    [Fact]
    public void ChangeRequesterSettings_Unchanged_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsRequesterSelectable(false).WithRequesterMayAddParticipants(true).Generate();

        // Act
        sut.ChangeRequesterSettings(false, true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeRequesterSettings

    #region ChangeMembers

    [Fact]
    public void ChangeMembers_AddingRemovingAndReRoling_RaisesOneEventWithEveryPart()
    {
        // Arrange
        var staying = Guid.NewGuid();
        var promoted = Guid.NewGuid();
        var leaving = Guid.NewGuid();
        var joining = Guid.NewGuid();
        var sut = new IntakeDeskFaker()
            .WithMembers(new(staying, IntakeDeskRole.Agent), new(promoted, IntakeDeskRole.Agent), new(leaving, IntakeDeskRole.Admin))
            .Generate();

        // Act
        var result = sut.ChangeMembers(
            [new(staying, IntakeDeskRole.Agent), new(promoted, IntakeDeskRole.Admin), new(joining, IntakeDeskRole.Agent)],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Members.Select(m => (m.EmployeeId, m.Role)).Should().BeEquivalentTo(
            [(staying, IntakeDeskRole.Agent), (promoted, IntakeDeskRole.Admin), (joining, IntakeDeskRole.Agent)]);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskMembersChangedEvent>().Subject;
        changed.Added.Should().Equal(new IntakeDeskMemberDetails(joining, IntakeDeskRole.Agent));
        changed.Removed.Should().Equal(new IntakeDeskMemberDetails(leaving, IntakeDeskRole.Admin));
        changed.RoleChanges.Should().Equal(new IntakeDeskRoleChange(promoted, IntakeDeskRole.Agent, IntakeDeskRole.Admin));
        changed.Members.Should().HaveCount(3);
    }

    [Fact]
    public void ChangeMembers_ToTheSameMembers_RaisesNothing()
    {
        // Arrange
        var agent = Guid.NewGuid();
        var sut = new IntakeDeskFaker().WithAgents(agent).Generate();

        // Act
        sut.ChangeMembers([new(agent, IntakeDeskRole.Agent)], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeMembers_GivingSomeoneTwoRoles_FailsAndChangesNothing()
    {
        // Arrange
        var agent = Guid.NewGuid();
        var sut = new IntakeDeskFaker().WithAgents(agent).Generate();

        // Act
        var result = sut.ChangeMembers([new(agent, IntakeDeskRole.Agent), new(agent, IntakeDeskRole.Admin)],
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An employee can hold only one role on a desk.");
        sut.Members.Should().ContainSingle().Which.Role.Should().Be(IntakeDeskRole.Agent);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IntakeDeskRole.Agent, IntakeDeskRole.Agent, false)]
    [InlineData(IntakeDeskRole.Agent, IntakeDeskRole.Admin, true)]
    [InlineData(IntakeDeskRole.Admin, IntakeDeskRole.Agent, true)]
    public void MembershipChange_ReRoling_TouchesDeskAdminsOnlyWhenAdminIsAnEnd(
        IntakeDeskRole from, IntakeDeskRole to, bool expected)
    {
        // Arrange
        var member = Guid.NewGuid();
        var sut = new IntakeDeskFaker().WithMembers(new IntakeDeskMemberDetails(member, from)).Generate();

        // Act
        var change = sut.MembershipChange([new(member, to)]);

        // Assert
        change.TouchesDeskAdmins.Should().Be(expected);
    }

    [Theory]
    [InlineData(IntakeDeskRole.Agent, false)]
    [InlineData(IntakeDeskRole.Admin, true)]
    public void MembershipChange_AddingOrRemoving_TouchesDeskAdminsOnlyForADeskAdmin(IntakeDeskRole role, bool expected)
    {
        // Arrange
        var existing = Guid.NewGuid();
        var sut = new IntakeDeskFaker().WithMembers(new IntakeDeskMemberDetails(existing, role)).Generate();

        // Act
        var removing = sut.MembershipChange([]);
        var adding = sut.MembershipChange([new(existing, role), new(Guid.NewGuid(), role)]);

        // Assert
        removing.TouchesDeskAdmins.Should().Be(expected);
        adding.TouchesDeskAdmins.Should().Be(expected);
    }

    #endregion ChangeMembers

    #region Revision

    [Fact]
    public void Revision_AdvancesOncePerRecordedChange()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithIsOpen(true).WithVisibility(IntakeDeskVisibility.Open).Generate();
        var start = sut.Revision;

        // Act
        sut.Close(EventActor.System, _dateTimeProvider.Now);
        sut.ChangeVisibility(IntakeDeskVisibility.Private, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Revision.Should().Be(start + 2);
        sut.IsAtRevision(start).Should().BeFalse();
        sut.IsAtRevision(start + 2).Should().BeTrue();
    }

    [Fact]
    public void Revision_StaysPutWhenNothingChanged()
    {
        // Arrange — a save that changes nothing must not make a page loaded beside it stale
        var sut = new IntakeDeskFaker().WithIsOpen(true).Generate();
        var start = sut.Revision;

        // Act
        sut.Open(EventActor.System, _dateTimeProvider.Now);
        sut.UpdateDetails(sut.Name, sut.Description, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Revision.Should().Be(start);
    }

    #endregion Revision

    #region Delete

    [Fact]
    public void Delete_RecordsTheDeskByName()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithName("IT").Generate();

        // Act
        sut.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskDeletedEvent>().Subject;
        deleted.Id.Should().Be(sut.Id);
        deleted.Name.Should().Be("IT");
    }

    #endregion Delete

    #region Channels

    [Fact]
    public void Create_OffersEveryActiveChannel()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var active = new IntakeChannelFaker().Generate();
        var archived = new IntakeChannelFaker().AsArchived().Generate();

        // Act
        var offered = sut.OfferedFrom([active, archived]);

        // Assert
        sut.OffersAllChannels.Should().BeTrue();
        offered.Should().Equal(active);
    }

    [Fact]
    public void ChangeChannels_ToChosenOnes_OffersOnlyThose()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var chosen = new IntakeChannelFaker().Generate();
        var other = new IntakeChannelFaker().Generate();

        // Act
        var result = sut.ChangeChannels(false, [chosen.Id], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Offers(chosen).Should().BeTrue();
        sut.Offers(other).Should().BeFalse();
        sut.Revision.Should().Be(1);
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskChannelsChangedEvent>().Subject;
        changed.OffersAllChannels.Should().BeFalse();
        changed.PreviousOffersAllChannels.Should().BeTrue();
        changed.AddedChannelIds.Should().Equal(chosen.Id);
        changed.RemovedChannelIds.Should().BeEmpty();
        changed.ChannelIds.Should().Equal(chosen.Id);
    }

    [Fact]
    public void ChangeChannels_ToNone_OffersNoChannel()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();

        // Act
        sut.ChangeChannels(false, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.OfferedFrom([new IntakeChannelFaker().Generate()]).Should().BeEmpty();
    }

    [Fact]
    public void ChangeChannels_ChosenChannelArchived_IsNotOffered()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var archived = new IntakeChannelFaker().AsArchived().Generate();
        sut.ChangeChannels(false, [archived.Id], EventActor.System, _dateTimeProvider.Now);

        // Act
        var offers = sut.Offers(archived);

        // Assert
        offers.Should().BeFalse();
        sut.Channels.Should().ContainSingle(c => c.ChannelId == archived.Id);
    }

    [Fact]
    public void ChangeChannels_BackToAll_ClearsTheChosenListWithBothEnds()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var chosen = Guid.NewGuid();
        sut.ChangeChannels(false, [chosen], EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        sut.ChangeChannels(true, [Guid.NewGuid()], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.OffersAllChannels.Should().BeTrue();
        sut.Channels.Should().BeEmpty();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskChannelsChangedEvent>().Subject;
        changed.PreviousOffersAllChannels.Should().BeFalse();
        changed.RemovedChannelIds.Should().Equal(chosen);
        changed.ChannelIds.Should().BeEmpty();
    }

    [Fact]
    public void ChangeChannels_ToTheSameChoice_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var chosen = Guid.NewGuid();
        sut.ChangeChannels(false, [chosen], EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeChannels(false, [chosen, chosen], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
        sut.Revision.Should().Be(1);
    }

    #endregion Channels


    #region Tag categories

    [Fact]
    public void Offers_ANewDesk_OffersEveryActiveAxis()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();

        // Act
        var offersActive = sut.Offers(new IntakeRequestTagCategoryFaker().Generate());
        var offersInactive = sut.Offers(new IntakeRequestTagCategoryFaker().WithIsActive(false).Generate());

        // Assert
        offersActive.Should().BeTrue();
        offersInactive.Should().BeFalse();
    }

    [Fact]
    public void ChangeTagCategories_ToChosenOnes_OffersOnlyThoseAndRaisesBothEnds()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var chosen = new IntakeRequestTagCategoryFaker().Generate();
        var other = new IntakeRequestTagCategoryFaker().Generate();

        // Act
        var result = sut.ChangeTagCategories(false, [chosen.Id], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Offers(chosen).Should().BeTrue();
        sut.Offers(other).Should().BeFalse();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskTagCategoriesChangedEvent>().Subject;
        changed.OffersAllTagCategories.Should().BeFalse();
        changed.PreviousOffersAllTagCategories.Should().BeTrue();
        changed.AddedTagCategoryIds.Should().Equal(chosen.Id);
        changed.RemovedTagCategoryIds.Should().BeEmpty();
        changed.TagCategoryIds.Should().Equal(chosen.Id);
    }

    [Fact]
    public void ChangeTagCategories_ChosenAxisDeactivated_IsNotOfferedButStaysChosen()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var inactive = new IntakeRequestTagCategoryFaker().WithIsActive(false).Generate();
        sut.ChangeTagCategories(false, [inactive.Id], EventActor.System, _dateTimeProvider.Now);

        // Act
        var offers = sut.Offers(inactive);

        // Assert
        offers.Should().BeFalse();
        sut.TagCategories.Should().ContainSingle(c => c.CategoryId == inactive.Id);
    }

    [Fact]
    public void ChangeTagCategories_BackToAll_ClearsTheChosenList()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();
        var chosen = new IntakeRequestTagCategoryFaker().Generate();
        sut.ChangeTagCategories(false, [chosen.Id], EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        sut.ChangeTagCategories(true, [chosen.Id], EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.TagCategories.Should().BeEmpty();
        var changed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskTagCategoriesChangedEvent>().Subject;
        changed.RemovedTagCategoryIds.Should().Equal(chosen.Id);
    }

    [Fact]
    public void ChangeTagCategories_WhenNothingChanges_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeDeskFaker().Generate();

        // Act
        var result = sut.ChangeTagCategories(true, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Tag categories

    #region Scoring model

    private readonly ScoringModelFaker _scoringModelFaker = new();

    [Fact]
    public void ChangeScoringModel_ToAnActiveModel_AssignsItAndRaisesTheChange()
    {
        // Arrange
        var model = _scoringModelFaker.AsActiveWsjf();
        var sut = new IntakeDeskFaker().Generate();
        var revision = sut.Revision;

        // Act
        var result = sut.ChangeScoringModel(model, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ScoringModelId.Should().Be(model.Id);
        sut.ScoresWith(model).Should().BeTrue();
        sut.Revision.Should().Be(revision + 1);
        var raised = sut.DomainEvents.OfType<IntakeDeskScoringModelChangedEvent>().Should().ContainSingle().Subject;
        raised.FromScoringModelId.Should().BeNull();
        raised.FromScoringModelName.Should().BeNull();
        raised.ToScoringModelId.Should().Be(model.Id);
        raised.ToScoringModelName.Should().Be(model.Name);
    }

    [Fact]
    public void ChangeScoringModel_ToAnotherModel_CarriesBothEnds()
    {
        // Arrange
        var from = _scoringModelFaker.AsActiveWsjf();
        var to = _scoringModelFaker.AsActiveWsjf();
        var sut = new IntakeDeskFaker().WithScoringModel(from).Generate();

        // Act
        var result = sut.ChangeScoringModel(to, from, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ScoringModelId.Should().Be(to.Id);
        var raised = sut.DomainEvents.OfType<IntakeDeskScoringModelChangedEvent>().Should().ContainSingle().Subject;
        raised.FromScoringModelId.Should().Be(from.Id);
        raised.FromScoringModelName.Should().Be(from.Name);
        raised.ToScoringModelId.Should().Be(to.Id);
    }

    [Fact]
    public void ChangeScoringModel_ToNull_StopsScoring()
    {
        // Arrange
        var from = _scoringModelFaker.AsActiveWsjf();
        var sut = new IntakeDeskFaker().WithScoringModel(from).Generate();

        // Act
        var result = sut.ChangeScoringModel(null, from, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ScoringModelId.Should().BeNull();
        var raised = sut.DomainEvents.OfType<IntakeDeskScoringModelChangedEvent>().Should().ContainSingle().Subject;
        raised.FromScoringModelId.Should().Be(from.Id);
        raised.ToScoringModelId.Should().BeNull();
    }

    [Fact]
    public void ChangeScoringModel_ToTheSameModel_ChangesNothing()
    {
        // Arrange
        var model = _scoringModelFaker.AsActiveWsjf();
        var sut = new IntakeDeskFaker().WithScoringModel(model).Generate();
        var revision = sut.Revision;

        // Act
        var result = sut.ChangeScoringModel(model, model, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Revision.Should().Be(revision);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeScoringModel_KeepingAModelArchivedSinceItWasChosen_ChangesNothing()
    {
        // Arrange — the settings form offers the desk's archived model, and saving it untouched must not fail
        var model = _scoringModelFaker.AsActiveWsjf();
        var sut = new IntakeDeskFaker().WithScoringModel(model).Generate();
        model.Archive(EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.ChangeScoringModel(model, model, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ScoringModelId.Should().Be(model.Id);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ScoringModelState.Proposed)]
    [InlineData(ScoringModelState.Archived)]
    public void ChangeScoringModel_ToAModelThatIsNotActive_Fails(ScoringModelState state)
    {
        // Arrange
        var model = state == ScoringModelState.Proposed ? _scoringModelFaker.AsProposedWsjf() : _scoringModelFaker.AsActiveWsjf();
        if (state == ScoringModelState.Archived)
            model.Archive(EventActor.System, _dateTimeProvider.Now);
        var sut = new IntakeDeskFaker().Generate();

        // Act
        var result = sut.ChangeScoringModel(model, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        sut.ScoringModelId.Should().BeNull();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeScoringModel_WhenTheCurrentModelIsNotTheDesks_Throws()
    {
        // Arrange
        var sut = new IntakeDeskFaker().WithScoringModel(_scoringModelFaker.AsActiveWsjf()).Generate();

        // Act
        Action act = () => sut.ChangeScoringModel(null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Scoring model
}
