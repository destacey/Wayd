using FluentAssertions;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestProjectDeliverablesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestProjectDeliverablesQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ForAnAgent_ListsOnlyThatProjectsDeliverablesByName()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project("ATLAS");
        var reports = _scenario.Deliverable(project, "Reports", ProjectDeliverableStatusAlias.Descoped);
        var portal = _scenario.Deliverable(project, "Portal", ProjectDeliverableStatusAlias.Accepted);
        _scenario.Deliverable(_scenario.Project("BOREAL", "Borealis"), "Archive");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestProjectDeliverablesQuery(request.Id, project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(d => (d.Id, d.Name, d.StatusAlias)).Should().Equal(
            (portal.Id, "Portal", ProjectDeliverableStatusAlias.Accepted),
            (reports.Id, "Reports", ProjectDeliverableStatusAlias.Descoped));
    }

    [Fact]
    public async Task Handle_ForSomeoneWhoSeesTheRequestButIsNotAnAgent_ReturnsNothing()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(), requester, "Accepted");
        var project = _scenario.Project();
        _scenario.Deliverable(project);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestProjectDeliverablesQuery(request.Id, project.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForAProjectWithNoCopy_ReturnsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestProjectDeliverablesQuery(request.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
