using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class ChangeIntakeRequestAssigneeCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ChangeIntakeRequestAssigneeCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgentHoldingNoClaim_AssignsAnotherMemberOfTheDesk()
    {
        // Arrange — membership is the grant, so the agent needs no permission beside it
        var agent = _scenario.Employee();
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id, assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssigneeId.Should().Be(assignee.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdminOffTheDesk_Assigns()
    {
        // Arrange
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssigneeId.Should().Be(assignee.Id);
    }

    [Fact]
    public async Task Handle_ByAViewHolderNotOnTheDesk_FailsAndSavesNothing()
    {
        // Arrange — View browses the request but acts on nothing
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.AssigneeId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAnAgentOfAnotherDesk_Fails()
    {
        // Arrange
        var otherAgent = _scenario.Employee();
        _scenario.Desk(agentIds: [otherAgent.Id]);
        var assignee = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [assignee.Id]), _scenario.Employee());
        _scenario.SignedInAs(otherAgent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, assignee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }

    [Fact]
    public async Task Handle_ToADeskAdmin_Assigns()
    {
        // Arrange — any member of the desk can own a request, whatever their role
        var deskAdmin = _scenario.Employee();
        var request = _scenario.Request(_scenario.DeskAdministeredBy(deskAdmin.Id), _scenario.Employee());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, deskAdmin.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssigneeId.Should().Be(deskAdmin.Id);
    }

    [Fact]
    public async Task Handle_ToSomeoneNotOnTheDesk_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, _scenario.Employee().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The assignee must be an active member of the request's desk.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNoAssignee_Unassigns()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        request.ChangeAssignee(agent.Id, EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestAssigneeCommand(request.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.AssigneeId.Should().BeNull();
    }
}
