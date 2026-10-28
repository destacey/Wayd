using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class AddIntakeRequestOutcomeCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private AddIntakeRequestOutcomeCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgentForAnAcceptedRequest_RecordsTheOutcomeAddedByThem()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        var project = _scenario.Project();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestOutcomeCommand(request.Id, IntakeRequestOutcomeKind.ExistingProjectScope, project.Id, "The export screen"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var outcome = request.Outcomes.Should().ContainSingle().Subject;
        outcome.TargetId.Should().Be(project.Id);
        outcome.Kind.Should().Be(IntakeRequestOutcomeKind.ExistingProjectScope);
        outcome.AddedById.Should().Be(agent.Id);
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestOutcomeAddedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndSavesNothing()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(), requester, "Accepted");
        var project = _scenario.Project();
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestOutcomeCommand(request.Id, IntakeRequestOutcomeKind.NewProject, project.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Outcomes.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAProjectWithNoCopy_FailsAndSavesNothing()
    {
        // Arrange — a project PPM has not replicated yet, or one that does not exist
        var agent = _scenario.Employee();
        var request = _scenario.RequestInStatus(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), "Accepted");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestOutcomeCommand(request.Id, IntakeRequestOutcomeKind.NewProject, Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AddIntakeRequestOutcomeCommandHandler.ProjectNotFoundError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForARequestNotYetAccepted_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.RequestIn(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), StatusCategory.Proposed);
        var project = _scenario.Project();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestOutcomeCommand(request.Id, IntakeRequestOutcomeKind.NewProject, project.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Domain.Models.IntakeRequest.OutcomeNeedsAcceptedRequestError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.RequestInStatus(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee(), "Accepted");
        var project = _scenario.Project();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestOutcomeCommand(request.Id, IntakeRequestOutcomeKind.NewProject, project.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }
}
