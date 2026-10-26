using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class ChangeIntakeRequestStatusCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ChangeIntakeRequestStatusCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _scenario.Statuses.Object);

    private Guid StatusId(string name) => _scenario.Workflow.Status(name).Id;

    [Fact]
    public async Task Handle_ByAnAgentOnTheDeskWithNoClaim_MovesTheRequest()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("In Review"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusId.Should().Be(StatusId("In Review"));
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestStatusChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_MovesTheRequest()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Accepted);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndChangesNothing()
    {
        // Arrange — the requester reads the status; the desk decides it
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_ToAStatusOfAnotherWorkflow_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, IntakeWorkflowFactory.Default("Elsewhere").Status("Accepted").Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"That status does not belong to '{_scenario.Workflow.Name}'.");
    }

    [Fact]
    public async Task Handle_ToDuplicate_OfARequestTheAgentCanSee_RecordsIt()
    {
        // Arrange — the surviving request is on another desk the agent does not work
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var survivor = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.SignedInAs(agent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Duplicate"), "Same as the export ask", survivor.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.DuplicateOfId.Should().Be(survivor.Id);
        request.StatusReason.Should().Be("Same as the export ask");
    }

    [Fact]
    public async Task Handle_ToDuplicate_OfAPrivateRequestTheAgentCannotSee_ReportsItAsNotFound()
    {
        // Arrange — the answer is the same as for a request that does not exist
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var hidden = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(agent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Duplicate"), "Same ask", hidden.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChangeIntakeRequestStatusCommandHandler.SurvivorNotFoundError);
        request.DuplicateOfId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToDeclinedWithoutAReason_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Declined"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Moving this request to 'Declined' needs a reason the requester can read.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
