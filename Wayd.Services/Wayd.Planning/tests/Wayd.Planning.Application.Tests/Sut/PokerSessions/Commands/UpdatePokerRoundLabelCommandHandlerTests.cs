using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class UpdatePokerRoundLabelCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdatePokerRoundLabelCommandHandler _handler;
    private readonly Mock<ILogger<UpdatePokerRoundLabelCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;

    private readonly PokerSessionFaker _sessionFaker;

    public UpdatePokerRoundLabelCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdatePokerRoundLabelCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();

        _handler = new UpdatePokerRoundLabelCommandHandler(_dbContext, _access, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldUpdateLabel_WhenSessionIsActive()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Original Label", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();

        var command = new UpdatePokerRoundLabelCommand(session.Id, round.Id, "Updated Label");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Label.Should().Be("Updated Label");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyRoundLabelUpdated(session.Id, round.Id, "Updated Label"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowNullLabel()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Original Label", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();

        var command = new UpdatePokerRoundLabelCommand(session.Id, round.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Label.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyRoundLabelUpdated(session.Id, round.Id, null), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldTrimLabel()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Label", null, PokerSessionActors.Facilitator);
        var round = session.Rounds.First();

        var command = new UpdatePokerRoundLabelCommand(session.Id, round.Id, "  Trimmed Label  ");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Label.Should().Be("Trimmed Label");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new UpdatePokerRoundLabelCommand(Guid.NewGuid(), Guid.NewGuid(), "Label");

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

        var command = new UpdatePokerRoundLabelCommand(session.Id, Guid.NewGuid(), "Label");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionIsCompleted()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Completed).Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerRoundLabelCommand(session.Id, Guid.NewGuid(), "Label");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundLabelUpdated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
