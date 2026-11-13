using FluentAssertions;
using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Models.Estimation;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class PublishPokerRoundToIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _roundId = Guid.NewGuid();

    private PublishPokerRoundToIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private PublishPokerRoundToIntakeRequestCommand Publish(IntakeRequest request, IntakeDesk desk, DemandEstimationScale scale, string value) =>
        new(request.Id, desk.Id, scale.CurrentVersion.Id, value, _sessionId, _roundId);

    [Fact]
    public async Task Handle_ByAnAgent_WritesTheConsensusOverAManualEstimateAndNamesTheRound()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.Estimated(request, desk, "S");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, scale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.Estimate!.Value.Should().Be("L");
        var raised = request.DomainEvents.OfType<IntakeRequestEstimateChangedEvent>().Should().ContainSingle().Subject;
        raised.Source.Should().Be(IntakeRequestEstimateSource.PokerRound);
        raised.PokerRound.Should().Be(new IntakeRequestEstimatePokerRound(_sessionId, _roundId));
        raised.Actor.EmployeeId.Should().Be(agent.Id);
        raised.Actor.UserId.Should().Be("user-1");
        raised.Timestamp.Should().Be(_scenario.Clock.Now);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithACard_ClearsTheEstimateAndRecordsTheCard()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.Estimated(request, desk, "M");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, scale, "?"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.Estimate.Should().BeNull();
        request.EstimateCard.Should().Be("?");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAFacilitatorWhoIsNotAnAgentOnTheDesk_RefusesAndSavesNothing()
    {
        // Arrange — invited to facilitate, and able to read the open desk's request, but not on the desk
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(), scale);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, scale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PublishPokerRoundToIntakeRequestCommandHandler.NotAnAgentError);
        request.Estimate.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForARequestTheFacilitatorCannotSee_RefusesWithoutConfirmingItExists()
    {
        // Arrange
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(IntakeDeskVisibility.Private), scale);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, scale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PublishPokerRoundToIntakeRequestCommandHandler.RequestUnavailableError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForARequestThatMovedToAnotherDesk_RefusesAndSavesNothing()
    {
        // Arrange — the agent works both desks, so only the move stands in the way
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var hostDesk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var otherDesk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var request = _scenario.Request(otherDesk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Publish(request, hostDesk, scale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PublishPokerRoundToIntakeRequestCommandHandler.RequestLeftDeskError);
        request.Estimate.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAClosedRequest_RefusesAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var request = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Done);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, scale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ClosedEstimateError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AfterTheDeskMovedOffTheSessionsVersion_RefusesAndSavesNothing()
    {
        // Arrange — the session voted on a version of another scale than the desk now pins
        var agent = _scenario.Employee();
        var sessionScale = _scenario.EstimationScale("Fibonacci");
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Publish(request, desk, sessionScale, "L"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.DeskLeftSessionScaleError);
        request.Estimate.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
