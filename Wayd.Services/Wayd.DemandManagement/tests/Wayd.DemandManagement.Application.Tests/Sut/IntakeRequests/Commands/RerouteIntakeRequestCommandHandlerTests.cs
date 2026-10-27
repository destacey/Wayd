using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class RerouteIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private RerouteIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _scenario.Statuses.Object);

    [Fact]
    public async Task Handle_ByAnAgentOnTheCurrentDesk_MovesItToADeskTheyDoNotWork()
    {
        // Arrange — an agent sends a request to the desk that should own it
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var destination = _scenario.Desk(agentIds: _scenario.Employee().Id);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, destination.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.DeskId.Should().Be(destination.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAnAgentOfTheDestinationOnly_FailsAndStays()
    {
        // Arrange — re-routing belongs to whoever owns the request now
        var destinationAgent = _scenario.Employee();
        var current = _scenario.Desk();
        var request = _scenario.Request(current, _scenario.Employee());
        var destination = _scenario.Desk(agentIds: destinationAgent.Id);
        _scenario.SignedInAs(destinationAgent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, destination.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.DeskId.Should().Be(current.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByTheRequester_Fails()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        var destination = _scenario.Desk();
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, destination.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }

    [Fact]
    public async Task Handle_ToALessPrivateDeskWithoutConfirming_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var current = _scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id);
        var request = _scenario.Request(current, _scenario.Employee());
        var destination = _scenario.Desk(IntakeDeskVisibility.Open);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, destination.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.WiderVisibilityNotConfirmedError);
        request.DeskId.Should().Be(current.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToADeskThatDoesNotExist_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, Guid.NewGuid(), false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_WithoutAStatus_KeepsTheOneItHoldsOnADeskSharingItsWorkflow()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Waiting on Requester");
        var statusId = request.StatusId;
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, _scenario.Desk().Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusId.Should().Be(statusId);
    }

    [Fact]
    public async Task Handle_WithAnOpenStatus_ArrivesInIt()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var inReview = _scenario.Workflow.Status("In Review");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, _scenario.Desk().Id, false, inReview.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusId.Should().Be(inReview.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Declined")]
    [InlineData("Fulfilled")]
    public async Task Handle_WithAStatusItCannotArriveIn_FailsAndStays(string statusName)
    {
        // Arrange — a closed status, which needs a reason, is the new desk's call
        var agent = _scenario.Employee();
        var current = _scenario.Desk(agentIds: agent.Id);
        var request = _scenario.Request(current, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new RerouteIntakeRequestCommand(request.Id, _scenario.Desk().Id, false, _scenario.Workflow.Status(statusName).Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ArrivalStatusNotOpenError);
        request.DeskId.Should().Be(current.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithoutAStatus_WhenItsOwnCannotArrive_AsksForOne()
    {
        // Arrange — the new desk's workflow has no status matching the one it holds
        var agent = _scenario.Employee();
        var current = _scenario.Desk(agentIds: agent.Id);
        var request = _scenario.RequestOnAnotherWorkflow(current, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RerouteIntakeRequestCommand(request.Id, _scenario.Desk().Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RerouteIntakeRequestCommandHandler.ChooseArrivalStatusError);
        request.DeskId.Should().Be(current.Id);
    }
}
