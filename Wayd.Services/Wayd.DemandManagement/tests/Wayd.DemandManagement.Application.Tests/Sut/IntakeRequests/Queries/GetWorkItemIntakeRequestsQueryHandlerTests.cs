using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetWorkItemIntakeRequestsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetWorkItemIntakeRequestsQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ListsTheRequestsTheWorkItemDelivers_ByNumber()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        var workItemId = Guid.NewGuid();
        var first = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        var second = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        var unrelated = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        _scenario.Outcome(second, workItemId, agent, IntakeRequestOutcomeKind.WorkItem, "The export screen");
        _scenario.Outcome(first, workItemId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.Outcome(unrelated, Guid.NewGuid(), agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetWorkItemIntakeRequestsQuery(workItemId), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Request.Id).Should().Equal(first.Id, second.Id);
        result[1].Note.Should().Be("The export screen");
        result[0].Status.Id.Should().Be(first.StatusId);
    }

    [Fact]
    public async Task Handle_IgnoresAProjectOutcomeWithTheSameId()
    {
        // Arrange — ids are unique across modules in practice; the kind still decides what the outcome names
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        var targetId = Guid.NewGuid();
        var delivered = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        var project = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        _scenario.Outcome(delivered, targetId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.Outcome(project, targetId, agent, IntakeRequestOutcomeKind.NewProject);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetWorkItemIntakeRequestsQuery(targetId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Request.Id.Should().Be(delivered.Id);
    }

    [Fact]
    public async Task Handle_ForAReaderOutsideAPrivateDesk_LeavesItsRequestsOut()
    {
        // Arrange — the work item is visible to the reader, but the request behind it is not
        var agent = _scenario.Employee();
        var workItemId = Guid.NewGuid();
        var hidden = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var open = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.Outcome(hidden, workItemId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.Outcome(open, workItemId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetWorkItemIntakeRequestsQuery(workItemId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Request.Id.Should().Be(open.Id);
    }

    [Fact]
    public async Task Handle_ForAnAgentOnThePrivateDesk_IncludesItsRequests()
    {
        // Arrange
        var agent = _scenario.Employee();
        var workItemId = Guid.NewGuid();
        var request = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.Outcome(request, workItemId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetWorkItemIntakeRequestsQuery(workItemId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Request.Id.Should().Be(request.Id);
    }
}
