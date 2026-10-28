using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetProjectIntakeRequestsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetProjectIntakeRequestsQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ListsTheRequestsTheProjectCameFromOrAbsorbed_ByNumber()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        var project = _scenario.Project();
        var started = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        var absorbed = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        var unrelated = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        _scenario.Outcome(started, project.Id, agent);
        _scenario.Outcome(absorbed, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Export screen");
        _scenario.Outcome(unrelated, _scenario.Project("OTHER").Id, agent);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetProjectIntakeRequestsQuery(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Request.Id).Should().BeEquivalentTo([started.Id, absorbed.Id]);
        result.Select(r => r.Request.Key).Should().BeInAscendingOrder();
        result.Single(r => r.Request.Id == absorbed.Id).Should().BeEquivalentTo(new
        {
            Kind = IntakeRequestOutcomeKind.ExistingProjectScope,
            Note = "Export screen",
        });
        result.Single(r => r.Request.Id == started.Id).Status.Id.Should().Be(started.StatusId);
    }

    [Fact]
    public async Task Handle_ForAReaderOutsideAPrivateDesk_LeavesItsRequestsOut()
    {
        // Arrange — the project is visible to the reader, but the request behind it is not
        var agent = _scenario.Employee();
        var project = _scenario.Project();
        var hidden = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var open = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.Outcome(hidden, project.Id, agent);
        _scenario.Outcome(open, project.Id, agent);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetProjectIntakeRequestsQuery(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Request.Id.Should().Be(open.Id);
    }

    [Fact]
    public async Task Handle_ForAnAgentOnThePrivateDesk_IncludesItsRequests()
    {
        // Arrange
        var agent = _scenario.Employee();
        var project = _scenario.Project();
        var request = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.Outcome(request, project.Id, agent);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetProjectIntakeRequestsQuery(project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Request.Id.Should().Be(request.Id);
    }
}
