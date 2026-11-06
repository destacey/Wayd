using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Events;
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
    private readonly StatusRemapDecisionDto[] _decisions = [new() { FromStatusId = Guid.NewGuid(), ToStatusId = Guid.NewGuid() }];

    public ChangeIntakeDeskWorkflowCommandHandlerTests()
    {
        _assignments.Setup(a => a.Change(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<StatusRemapDecisionDto>>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(3));
    }

    private ChangeIntakeDeskWorkflowCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _assignments.Object);

    private ChangeIntakeDeskWorkflowCommand Command(Guid deskId) => new(deskId, _workflowId, _decisions);

    private void VerifyChanged(Guid deskId, Times times) =>
        _assignments.Verify(a => a.Change(DemandManagementWorkflowOwners.IntakeRequest.Key, deskId, _workflowId, _decisions,
            It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Handle_ByADeskAdminWithNoClaim_ChangesTheDesksWorkflowWithTheDecisions()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
        VerifyChanged(desk.Id, Times.Once());
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_ChangesTheDesksWorkflow()
    {
        // Arrange
        var desk = _scenario.Desk();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyChanged(desk.Id, Times.Once());
    }

    [Fact]
    public async Task Handle_ForADeskHoldingRequests_ChangesItsWorkflow()
    {
        // Arrange — the requests move with it
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.RequestInStatus(desk, _scenario.Employee(), "Declined");
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyChanged(desk.Id, Times.Once());
    }

    [Fact]
    public async Task Handle_ByAnAgent_FailsAndChangesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        VerifyChanged(desk.Id, Times.Never());
    }

    [Fact]
    public async Task Handle_WhenTheChangeIsRefused_ReportsWhy()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);
        _assignments.Setup(a => a.Change(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<StatusRemapDecisionDto>>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>("These statuses have nowhere to go."));

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("These statuses have nowhere to go.");
    }

    [Fact]
    public async Task Handle_WhenAnotherChangeSavedFirst_ReportsTheDeskAsStale()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);
        _assignments.Setup(a => a.Change(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<StatusRemapDecisionDto>>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>(ScopedWorkflowAssignments.ConcurrentChangeError));

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }
}
