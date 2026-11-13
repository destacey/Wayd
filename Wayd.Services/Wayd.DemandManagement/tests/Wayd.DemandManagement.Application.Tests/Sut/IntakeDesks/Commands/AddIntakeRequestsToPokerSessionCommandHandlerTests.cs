using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class AddIntakeRequestsToPokerSessionCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Guid _sessionId = Guid.NewGuid();
    private AddPokerRoundSubjectsCommand? _sent;

    public AddIntakeRequestsToPokerSessionCommandHandlerTests()
    {
        _dispatcher.Setup(d => d.Send(It.IsAny<AddPokerRoundSubjectsCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand command, CancellationToken _) => _sent = (AddPokerRoundSubjectsCommand)command)
            .ReturnsAsync(Result.Success());
    }

    private AddIntakeRequestsToPokerSessionCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _dispatcher.Object);

    [Fact]
    public async Task Handle_ByAnAgent_AsksPlanningForARoundPerRequestInTheDesksSession()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestsToPokerSessionCommand(desk.Id, _sessionId, [request.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _sent.Should().NotBeNull();
        _sent!.SessionId.Should().Be(_sessionId);
        _sent.HostKind.Should().Be(PokerSessionHostKind.IntakeDesk);
        _sent.HostId.Should().Be(desk.Id);
        _sent.Subjects.Should().Equal(
            new PokerRoundSubjectInput(PokerRoundSubjectKind.IntakeRequest, request.Id, request.Key.ToString(), request.Title, request.Description));
    }

    [Fact]
    public async Task Handle_BySomeoneOffTheDesk_FailsAndAddsNothing()
    {
        // Arrange
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestsToPokerSessionCommand(desk.Id, _sessionId, [request.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.NotAnAgentError);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithARequestOnAnotherDesk_FailsAndAddsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: agent.Id), _scenario.EstimationScale());
        var elsewhere = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new AddIntakeRequestsToPokerSessionCommand(desk.Id, _sessionId, [elsewhere.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskPokerSessions.NotOpenRequestsOfDeskError);
        _dispatcher.VerifyNoOtherCalls();
    }
}
