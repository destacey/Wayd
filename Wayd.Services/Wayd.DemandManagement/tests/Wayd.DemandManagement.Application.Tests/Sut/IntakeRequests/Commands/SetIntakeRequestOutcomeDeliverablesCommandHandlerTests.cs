using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class SetIntakeRequestOutcomeDeliverablesCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private SetIntakeRequestOutcomeDeliverablesCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgent_ReplacesTheMappingAndSaves()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project();
        var dropped = _scenario.Deliverable(project, "Portal");
        var added = _scenario.Deliverable(project, "Reports");
        _scenario.Outcome(request, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export", [dropped.Id]);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new SetIntakeRequestOutcomeDeliverablesCommand(request.Id, project.Id, [added.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Outcomes.Single().DeliverableIds().Should().Equal(added.Id);
        var changed = request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestOutcomeDeliverablesChangedEvent>().Subject;
        changed.Added.Should().Equal(added.Id);
        changed.Removed.Should().Equal(dropped.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeepingADeliverableDeletedFromTheProject_IsAllowed()
    {
        // Arrange — mapped before it was deleted, so it has no copy but stays mapped.
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project();
        var deleted = Guid.NewGuid();
        var added = _scenario.Deliverable(project);
        _scenario.Outcome(request, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export", [deleted]);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new SetIntakeRequestOutcomeDeliverablesCommand(request.Id, project.Id, [deleted, added.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Outcomes.Single().DeliverableIds().Should().BeEquivalentTo([deleted, added.Id]);
    }

    [Fact]
    public async Task Handle_WithADeliverableNotOnTheProject_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project("ATLAS");
        var theirs = _scenario.Deliverable(_scenario.Project("BOREAL", "Borealis"));
        _scenario.Outcome(request, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new SetIntakeRequestOutcomeDeliverablesCommand(request.Id, project.Id, [theirs.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith(IntakeRequestAccess.DeliverablesNotOnProjectError);
        request.Outcomes.Single().Deliverables.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), requester, "Accepted");
        var project = _scenario.Project();
        var deliverable = _scenario.Deliverable(project);
        _scenario.Outcome(request, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export");
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(
            new SetIntakeRequestOutcomeDeliverablesCommand(request.Id, project.Id, [deliverable.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnOutcomeTheRequestDoesNotHave_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new SetIntakeRequestOutcomeDeliverablesCommand(request.Id, Guid.NewGuid(), []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.OutcomeNotFoundError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
