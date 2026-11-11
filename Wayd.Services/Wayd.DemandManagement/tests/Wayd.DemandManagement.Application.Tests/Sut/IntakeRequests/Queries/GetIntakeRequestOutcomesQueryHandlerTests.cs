using FluentAssertions;
using Moq;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestOutcomesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestOutcomesQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_Mine_ReturnsTheOutcomesOfTheCallersRequestsWithTheirProgress()
    {
        // Arrange
        var caller = _scenario.Employee();
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id);
        var project = _scenario.Project(name: "Atlas");
        var accepted = _scenario.Deliverable(project, "Export", ProjectDeliverableStatusAlias.Accepted);
        var inProgress = _scenario.Deliverable(project, "Import", ProjectDeliverableStatusAlias.InProgress);
        var workItemId = Guid.CreateVersion7();

        var request = _scenario.RequestInStatus(desk, caller, "Accepted");
        _scenario.Outcome(request, project.Id, agent, IntakeRequestOutcomeKind.ExistingProjectScope, "Exports",
            [accepted.Id, inProgress.Id]);
        _scenario.Outcome(request, workItemId, agent, IntakeRequestOutcomeKind.WorkItem);
        _scenario.RequestInStatus(desk, caller, "Accepted");
        var someoneElses = _scenario.RequestInStatus(desk, _scenario.Employee(), "Accepted");
        _scenario.Outcome(someoneElses, project.Id, agent);
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestOutcomesQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert — the request with no outcome is absent, and so is someone else's
        var outcomes = result.Should().ContainSingle().Subject;
        outcomes.RequestId.Should().Be(request.Id);
        outcomes.DeliverablesMet.Should().BeFalse();
        outcomes.Outcomes.Should().HaveCount(2);
        var scope = outcomes.Outcomes.Single(o => o.Kind == IntakeRequestOutcomeKind.ExistingProjectScope);
        scope.Project!.Name.Should().Be("Atlas");
        scope.IsMet.Should().BeFalse();
        scope.Deliverables.Select(d => d.Name).Should().Equal("Export", "Import");
        outcomes.Outcomes.Single(o => o.Kind == IntakeRequestOutcomeKind.WorkItem).TargetId.Should().Be(workItemId);
    }

    [Fact]
    public async Task Handle_ForAnOutsider_LeavesOutAPrivateDesksRequests()
    {
        // Arrange
        var agent = _scenario.Employee();
        var project = _scenario.Project();
        var onPrivate = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.Outcome(onPrivate, project.Id, agent);
        var onOpen = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee(), "Accepted");
        _scenario.Outcome(onOpen, project.Id, agent);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestOutcomesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.RequestId).Should().Equal(onOpen.Id);
    }

    [Fact]
    public async Task Handle_MineWithNoLinkedEmployee_ReturnsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee(), "Accepted");
        _scenario.Outcome(request, _scenario.Project().Id, agent);
        _scenario.Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestOutcomesQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
