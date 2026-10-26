using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class ChangeIntakeDeskWorkflowCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IScopedWorkflowAssignments> _assignments = new();
    private readonly Guid _workflowId = Guid.NewGuid();

    public ChangeIntakeDeskWorkflowCommandHandlerTests()
    {
        _assignments.Setup(a => a.AssignToEmptyScope(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<EventActor>(),
                It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private ChangeIntakeDeskWorkflowCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _assignments.Object);

    private void VerifyAssigned(Guid deskId, Times times) =>
        _assignments.Verify(a => a.AssignToEmptyScope(DemandManagementWorkflowOwners.IntakeRequest.Key, deskId, _workflowId,
            It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Handle_ByADeskAdminWithNoClaim_PointsTheEmptyDeskAtTheWorkflow()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskWorkflowCommand(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyAssigned(desk.Id, Times.Once());
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_PointsTheEmptyDeskAtTheWorkflow()
    {
        // Arrange
        var desk = _scenario.Desk();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskWorkflowCommand(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyAssigned(desk.Id, Times.Once());
    }

    [Fact]
    public async Task Handle_ByAnAgent_FailsAndAssignsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskWorkflowCommand(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        VerifyAssigned(desk.Id, Times.Never());
    }

    [Fact]
    public async Task Handle_ForADeskHoldingARequest_FailsAndAssignsNothing()
    {
        // Arrange — even a closed one: its history is on the current workflow
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.RequestInStatus(desk, _scenario.Employee(), "Declined");
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskWorkflowCommand(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChangeIntakeDeskWorkflowCommandHandler.DeskHasRequestsError);
        VerifyAssigned(desk.Id, Times.Never());
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeDeskWorkflowCommand(Guid.NewGuid(), _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }
}
