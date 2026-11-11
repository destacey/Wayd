using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class FocusPokerRoundCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly FocusPokerRoundCommandHandler _handler;
    private readonly Mock<ILogger<FocusPokerRoundCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;

    private readonly PokerSessionFaker _sessionFaker;

    public FocusPokerRoundCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<FocusPokerRoundCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();

        _handler = new FocusPokerRoundCommandHandler(_dbContext, _access, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldFocusTheRoundAndTellTheSession()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("First", null, PokerSessionActors.Facilitator);
        var round = session.AddRound("Second", null, PokerSessionActors.Facilitator).Value;

        var command = new FocusPokerRoundCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(round.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(session.Id, round.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldClearTheFocus_WhenNoRoundIsGiven()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("First", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        var command = new FocusPokerRoundCommand(session.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(session.Id, null), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSaveAndTellNothing_WhenTheRoundIsAlreadyFocused()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("First", null, PokerSessionActors.Facilitator).Value;
        session.FocusRound(round.Id, PokerSessionActors.Facilitator);

        var command = new FocusPokerRoundCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.FocusedRoundId.Should().Be(round.Id);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReadAsNotFound_WhenTheCallerCannotSeeThePrivateSession()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).WithVisibility(Visibility.Private).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("First", null, PokerSessionActors.Facilitator).Value;
        _access.Actor = PokerSessionActors.Voter("someone");

        var command = new FocusPokerRoundCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionIsCompleted()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("First", null, PokerSessionActors.Facilitator).Value;
        session.Complete(PokerSessionActors.Facilitator, default);

        var command = new FocusPokerRoundCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.FocusedRoundId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheCallerIsNotAFacilitator()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("First", null, PokerSessionActors.Facilitator).Value;
        _access.Actor = PokerSessionActors.Voter("someone");

        var command = new FocusPokerRoundCommand(session.Id, round.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        session.FocusedRoundId.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new FocusPokerRoundCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoundDoesNotExist()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var command = new FocusPokerRoundCommand(session.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Round not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundFocused(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
