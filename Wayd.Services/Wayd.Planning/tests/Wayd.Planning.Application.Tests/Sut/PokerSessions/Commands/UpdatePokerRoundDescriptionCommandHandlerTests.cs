using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class UpdatePokerRoundDescriptionCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdatePokerRoundDescriptionCommandHandler _handler;
    private readonly Mock<ILogger<UpdatePokerRoundDescriptionCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;

    private readonly PokerSessionFaker _sessionFaker;

    public UpdatePokerRoundDescriptionCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdatePokerRoundDescriptionCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();

        _handler = new UpdatePokerRoundDescriptionCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
    }

    [Fact]
    public async Task Handle_ShouldUpdateDescription_WhenSessionIsActive()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("Label", "Original").Value;

        var command = new UpdatePokerRoundDescriptionCommand(session.Id, round.Id, "Updated");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Description.Should().Be("Updated");
        round.Label.Should().Be("Label");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyRoundDescriptionUpdated(session.Id, round.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldClearDescription_WhenNull()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Active).Generate();
        _dbContext.AddPokerSession(session);

        var round = session.AddRound("Label", "Original").Value;

        var command = new UpdatePokerRoundDescriptionCommand(session.Id, round.Id, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        round.Description.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new UpdatePokerRoundDescriptionCommand(Guid.NewGuid(), Guid.NewGuid(), "Description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionIsCompleted()
    {
        // Arrange
        var session = _sessionFaker.WithStatus(PokerSessionStatus.Completed).Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerRoundDescriptionCommand(session.Id, Guid.NewGuid(), "Description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyRoundDescriptionUpdated(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
