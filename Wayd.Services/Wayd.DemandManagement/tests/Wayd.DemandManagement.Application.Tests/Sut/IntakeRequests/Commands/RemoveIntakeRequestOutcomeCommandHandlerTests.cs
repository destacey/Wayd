using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class RemoveIntakeRequestOutcomeCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private RemoveIntakeRequestOutcomeCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgent_RemovesTheOutcome()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project();
        _scenario.Outcome(request, project.Id, agent);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestOutcomeCommand(request.Id, project.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Outcomes.Should().BeEmpty();
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestOutcomeRemovedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForAProjectSinceDeleted_StillRemovesTheOutcome()
    {
        // Arrange — the copy went with the project; the outcome stayed
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var deletedProjectId = Guid.NewGuid();
        _scenario.Outcome(request, deletedProjectId, agent);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestOutcomeCommand(request.Id, deletedProjectId),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndKeepsTheOutcome()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(), requester, "Accepted");
        var project = _scenario.Project();
        _scenario.Outcome(request, project.Id, _scenario.Employee());
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new RemoveIntakeRequestOutcomeCommand(request.Id, project.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Outcomes.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
