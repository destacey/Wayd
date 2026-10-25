using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

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
}
