using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class StartIntakeDeskPokerSessionCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly ObjectIdAndKey _started = new(Guid.NewGuid(), 7);
    private StartHostedPokerSessionCommand? _sent;

    public StartIntakeDeskPokerSessionCommandHandlerTests()
    {
        _dispatcher.Setup(d => d.Send(It.IsAny<StartHostedPokerSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand<ObjectIdAndKey> command, CancellationToken _) => _sent = (StartHostedPokerSessionCommand)command)
            .ReturnsAsync(Result.Success(_started));
    }

    private StartIntakeDeskPokerSessionCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _dispatcher.Object);

    [Fact]
    public async Task Handle_ByAnAgent_StartsASessionOnTheDesksVersionWithARoundPerRequestInTheOrderChosen()
    {
        // Arrange
        var agent = _scenario.Employee();
        var invitee = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var first = _scenario.Request(desk, _scenario.Employee());
        var second = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(desk.Id, "Desk refinement", [second.Id, first.Id], [invitee.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(_started);
        _sent.Should().NotBeNull();
        _sent!.Name.Should().Be("Desk refinement");
        _sent.HostKind.Should().Be(PokerSessionHostKind.IntakeDesk);
        _sent.HostId.Should().Be(desk.Id);
        _sent.EstimationScaleVersionId.Should().Be(scale.CurrentVersion.Id);
        _sent.InvitedEmployeeIds.Should().Equal(invitee.Id);
        _sent.Subjects.Should().Equal(
            new PokerRoundSubjectInput(PokerRoundSubjectKind.IntakeRequest, second.Id, second.Key.ToString(), second.Title, second.Description),
            new PokerRoundSubjectInput(PokerRoundSubjectKind.IntakeRequest, first.Id, first.Key.ToString(), first.Title, first.Description));
    }

    [Fact]
    public async Task Handle_BySomeoneOffTheDesk_FailsAndStartsNothing()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(desk.Id, "Desk refinement", [request.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.NotAnAgentError);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForADeskWithNoScale_FailsAndStartsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(desk.Id, "Desk refinement", []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.DeskDoesNotEstimateError);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithARequestOnAnotherDesk_FailsAndStartsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), scale);
        var own = _scenario.Request(desk, _scenario.Employee());
        var elsewhere = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(desk.Id, "Desk refinement", [own.Id, elsewhere.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.NotOpenRequestsOfDeskError);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithAClosedRequest_FailsAndStartsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var closed = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Done);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(desk.Id, "Desk refinement", [closed.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.NotOpenRequestsOfDeskError);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAMissingDesk_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(
            new StartIntakeDeskPokerSessionCommand(Guid.NewGuid(), "Desk refinement", []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }
}
