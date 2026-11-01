using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class DeletePokerSessionCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeletePokerSessionCommandHandler _handler;
    private readonly Mock<ILogger<DeletePokerSessionCommandHandler>> _mockLogger;

    private readonly PokerSessionFaker _sessionFaker;
    private readonly EstimationScaleFaker _scaleFaker;

    public DeletePokerSessionCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeletePokerSessionCommandHandler>>();

        _handler = new DeletePokerSessionCommandHandler(_dbContext, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
        _scaleFaker = new EstimationScaleFaker();
    }

    [Fact]
    public async Task Handle_ShouldDeleteSession_WhenSessionIsActive()
    {
        // Arrange
        var scale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(scale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new DeletePokerSessionCommand(session.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteSession_WhenSessionIsCompleted()
    {
        // Arrange
        var scale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Completed)
            .WithEstimationScale(scale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new DeletePokerSessionCommand(session.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new DeletePokerSessionCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
