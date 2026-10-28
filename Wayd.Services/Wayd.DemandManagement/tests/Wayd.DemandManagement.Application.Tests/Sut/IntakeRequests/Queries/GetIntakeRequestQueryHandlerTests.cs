using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ForTheDemandManagementAdminOffADesk_OffersAssigning()
    {
        // Arrange — the admin acts as Desk Admin on every desk, so may assign without being named on it
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanChangeAssignee.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForTheDemandManagementAdminWithNoLinkedEmployee_OffersOnlyDelete()
    {
        // Arrange — the edit and participant commands need a linked employee, so the flags must not offer them;
        // deleting does not
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        _scenario.Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Substance.Should().NotBeNull();
        result.CanEdit.Should().BeFalse();
        result.CanRemoveParticipants.Should().BeFalse();
        result.CanAddParticipants.Should().BeFalse();
        result.CanDelete.Should().BeTrue();
    }

    [Theory]
    [InlineData(IntakeDeskRole.Agent, false)]
    [InlineData(IntakeDeskRole.Admin, true)]
    public async Task Handle_ForAMemberOfThePrivateDesk_OffersWhatTheirRoleAllows(IntakeDeskRole role, bool expectedDelete)
    {
        // Arrange — membership alone, with no claim, is what grants each action
        var member = _scenario.Employee();
        var desk = role == IntakeDeskRole.Admin
            ? _scenario.DeskAdministeredBy(member.Id)
            : _scenario.Desk(IntakeDeskVisibility.Private, agentIds: member.Id);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(member);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Substance.Should().NotBeNull();
        result.CanEdit.Should().BeTrue();
        result.CanRemoveParticipants.Should().BeTrue();
        result.CanAddParticipants.Should().BeTrue();
        result.CanDelete.Should().Be(expectedDelete);
    }

    [Fact]
    public async Task Handle_ForTheLinkedRequester_OffersEdit()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanEdit.Should().BeTrue();
        result.CanRemoveParticipants.Should().BeFalse();
        result.CanDelete.Should().BeFalse();
    }

    [Theory]
    [InlineData(IntakeDeskVisibility.Open, false, true)]
    [InlineData(IntakeDeskVisibility.Private, true, true)]
    [InlineData(IntakeDeskVisibility.Private, false, false)]
    public async Task Handle_ForTheRequester_OffersAddParticipantsWhereTheDeskAllowsIt(
        IntakeDeskVisibility visibility, bool requesterMayAddParticipants, bool expected)
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(visibility, requesterMayAddParticipants: requesterMayAddParticipants), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanAddParticipants.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ForAParticipant_DoesNotOfferAddParticipants()
    {
        // Arrange
        var participant = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee(), participant.Id);
        _scenario.SignedInAs(participant);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanAddParticipants.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForAnAgentOnAPrivateDeskThatDisallowsRequesters_OffersAddParticipants()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.Private, requesterMayAddParticipants: false, agentIds: agent.Id);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanAddParticipants.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForARequestTheReaderCannotSee_ReturnsNull()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForTheLinkedRequesterWithAReadOnlyToken_OffersNoChange()
    {
        // Arrange — the requester may edit by who they are, which a token's scope does not narrow on its own
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), requester);
        _scenario.SignedInAs(requester).WithReadOnlyToken();

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanEdit.Should().BeFalse();
        result.CanAddParticipants.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForADeskAdminWithAReadOnlyToken_ShowsThePrivateRequest_WithoutOfferingAnyChange()
    {
        // Arrange — membership widens what its holder sees, so a read-only token keeps that and loses only the actions
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin).WithReadOnlyToken();

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Substance.Should().NotBeNull();
        result.CanEdit.Should().BeFalse();
        result.CanRemoveParticipants.Should().BeFalse();
        result.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OffersRerouteToAnAgentOnTheDeskButNotToTheRequester()
    {
        // Arrange
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), requester);

        // Act
        _scenario.SignedInAs(agent);
        var asAgent = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(requester);
        var asRequester = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        asAgent!.CanReroute.Should().BeTrue();
        asRequester!.CanReroute.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForTheRequesterOnceAccepted_DoesNotOfferEdit_ButOffersItToAnAgent()
    {
        // Arrange
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.Desk(agentIds: agent.Id), requester, StatusCategory.Active, IntakeRequestStatusAlias.Accepted);

        // Act
        _scenario.SignedInAs(requester);
        var asRequester = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(agent);
        var asAgent = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        asRequester!.CanEdit.Should().BeFalse();
        asAgent!.CanEdit.Should().BeTrue();
        asAgent.CanReroute.Should().BeTrue("an accepted request is still open");
    }

    [Theory]
    [InlineData(StatusCategory.Done, IntakeRequestStatusAlias.Fulfilled)]
    [InlineData(StatusCategory.Removed, IntakeRequestStatusAlias.None)]
    public async Task Handle_ForAClosedRequest_OffersNeitherEditNorReroute_EvenToADeskAdmin(StatusCategory category, IntakeRequestStatusAlias alias)
    {
        // Arrange
        var admin = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.DeskAdministeredBy(admin.Id), _scenario.Employee(), category, alias);
        _scenario.SignedInAs(admin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanEdit.Should().BeFalse();
        result.CanReroute.Should().BeFalse();
        result.CanDelete.Should().BeFalse("it has moved through review");
    }

    [Fact]
    public async Task Handle_ForADeskAdmin_OffersDeleteOnlyWhileTheRequestHasNotLeftSubmitted()
    {
        // Arrange — back in an open status, but with review history behind it
        var admin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(admin.Id);
        var untouched = _scenario.Request(desk, _scenario.Employee());
        var reviewed = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted);
        _scenario.SignedInAs(admin);

        // Act
        var forUntouched = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(untouched.Id)), TestContext.Current.CancellationToken);
        var forReviewed = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(reviewed.Id)), TestContext.Current.CancellationToken);

        // Assert
        forUntouched!.CanDelete.Should().BeTrue();
        forReviewed!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OffersStatusAndAssigneeChangesToAnAgentAndWithdrawingToTheRequester()
    {
        // Arrange
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), requester);

        // Act
        _scenario.SignedInAs(agent);
        var asAgent = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(requester);
        var asRequester = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        asAgent!.CanChangeStatus.Should().BeTrue();
        asAgent.CanChangeAssignee.Should().BeTrue();
        asAgent.CanWithdraw.Should().BeFalse();
        asRequester!.CanChangeStatus.Should().BeFalse();
        asRequester.CanChangeAssignee.Should().BeFalse();
        asRequester.CanWithdraw.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DoesNotOfferWithdrawingOnceTheRequestIsClosed_NorToTheSubmitter()
    {
        // Arrange
        var requester = _scenario.Employee();
        var submitter = _scenario.Employee();
        var declined = _scenario.RequestInStatus(_scenario.Desk(), requester, "Declined");
        var onBehalf = _scenario.RequestOnBehalf(_scenario.Desk(), _scenario.Employee(), submitter);

        // Act
        _scenario.SignedInAs(requester);
        var forDeclined = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(declined.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(submitter);
        var forSubmitter = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(onBehalf.Id)), TestContext.Current.CancellationToken);

        // Assert
        forDeclined!.CanWithdraw.Should().BeFalse();
        forSubmitter!.CanWithdraw.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForAReadOnlyToken_OffersNoStatusOrAssigneeChangeNorWithdraw()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(requester.Id), requester);
        _scenario.SignedInAs(requester).WithReadOnlyToken();

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanChangeStatus.Should().BeFalse();
        result.CanChangeAssignee.Should().BeFalse();
        result.CanWithdraw.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForADuplicate_NamesTheSurvivingRequestOnlyToAReaderWhoCanSeeIt()
    {
        // Arrange — the survivor is on a private desk the duplicate's requester is not on
        var requester = _scenario.Employee();
        var privateAgent = _scenario.Employee();
        var survivor = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: privateAgent.Id), _scenario.Employee());
        var duplicate = _scenario.DuplicateOf(survivor, _scenario.Desk(agentIds: privateAgent.Id), requester);

        // Act
        _scenario.SignedInAs(requester, IntakeScenario.View);
        var asRequester = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(duplicate.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(privateAgent);
        var asAgent = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(duplicate.Id)), TestContext.Current.CancellationToken);

        // Assert
        asRequester!.DuplicateOf.Should().BeNull();
        asAgent!.DuplicateOf!.Id.Should().Be(survivor.Id);
        asAgent.DuplicateOf.Key.Should().Be(survivor.Key);
        asAgent.DuplicateOf.Title.Should().Be(survivor.Title);
    }

    [Fact]
    public async Task Handle_SaysWhetherTheReaderWorksTheDesk_WhateverTheirToken()
    {
        // Arrange — who the reader is, not what they may do: a read-only token keeps an agent an agent
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());

        // Act
        _scenario.SignedInAs(agent).WithReadOnlyToken();
        var asAgent = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);
        var asOutsider = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        asAgent!.IsDeskAgent.Should().BeTrue();
        asAgent.CanChangeStatus.Should().BeFalse();
        asAgent.CanChangeAssignee.Should().BeFalse();
        asOutsider!.IsDeskAgent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsTheStatusInTheSummary()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.Desk(), requester, StatusCategory.Proposed, IntakeRequestStatusAlias.WaitingOnRequester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Summary.Status.Id.Should().Be(request.StatusId);
        result.Summary.Status.Name.Should().Be(request.StatusName);
        result.Summary.Status.Category.Should().Be(StatusCategory.Proposed);
        result.Summary.Status.Alias.Should().Be((int)IntakeRequestStatusAlias.WaitingOnRequester);
    }

    [Fact]
    public async Task Handle_ForARequestWithOutcomes_ReturnsThemWithTheirProjectsToAnyoneWhoSeesTheSummary()
    {
        // Arrange — one project still exists; the other has been deleted, and only its copy went
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.SummaryOnly, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project("ATLAS", "Atlas");
        var deletedProjectId = Guid.NewGuid();
        _scenario.Outcome(request, project.Id, agent);
        _scenario.Outcome(request, deletedProjectId, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export screen");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Substance.Should().BeNull();
        result.Outcomes.Should().HaveCount(2);
        var live = result.Outcomes.Single(o => o.TargetId == project.Id);
        live.Project!.Key.Should().Be("ATLAS");
        live.Project.Name.Should().Be("Atlas");
        live.AddedBy.Id.Should().Be(agent.Id);
        var gone = result.Outcomes.Single(o => o.TargetId == deletedProjectId);
        gone.Project.Should().BeNull();
        gone.Note.Should().Be("Export screen");
        result.CanChangeOutcomes.Should().BeFalse();
    }

    [Theory]
    [InlineData("Accepted", true)]
    [InlineData("In Review", false)]
    [InlineData("Fulfilled", false)]
    public async Task Handle_ForAnAgent_OffersChangingOutcomesOnlyWhileAccepted(string statusName, bool expected)
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), statusName);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestQuery(new IdOrKey(request.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanChangeOutcomes.Should().Be(expected);
    }
}
