using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class ChangeIntakeRequestEstimateCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ChangeIntakeRequestEstimateCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgent_EstimatesOnTheDesksPinnedVersion()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.Estimate!.Value.Should().Be("M");
        request.Estimate.ScaleVersionId.Should().Be(scale.CurrentVersion.Id);
        var raised = request.DomainEvents.OfType<IntakeRequestEstimateChangedEvent>().Should().ContainSingle().Subject;
        raised.Source.Should().Be(IntakeRequestEstimateSource.Manual);
        raised.Actor.EmployeeId.Should().Be(agent.Id);
        raised.Actor.UserId.Should().Be("user-1");
        raised.Timestamp.Should().Be(_scenario.Clock.Now);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdminOffTheDesk_Estimates()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "XL"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.Estimate!.Value.Should().Be("XL");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoValue_ClearsTheEstimate()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.Estimated(request, desk, "S");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.Estimate.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndSavesNothing()
    {
        // Arrange — the requester may edit a Proposed request, but never size it
        var requester = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Estimate.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAnAgentOnAnotherDesk_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        _scenario.Desk(agentIds: agent.Id);
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForARequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithACard_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "?"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.NotAnEstimateValueError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnADeskWithNoScale_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.DeskDoesNotEstimateError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAClosedRequest_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var request = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Done);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ClosedEstimateError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNoLinkedEmployee_Throws()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());

        // Act
        var act = () => CreateHandler().Handle(new ChangeIntakeRequestEstimateCommand(request.Id, "M"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
