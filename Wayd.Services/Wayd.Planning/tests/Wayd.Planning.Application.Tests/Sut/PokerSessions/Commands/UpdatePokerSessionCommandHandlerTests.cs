using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class UpdatePokerSessionCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdatePokerSessionCommandHandler _handler;
    private readonly Mock<ILogger<UpdatePokerSessionCommandHandler>> _mockLogger;
    private readonly Mock<IPokerSessionNotifier> _mockNotifier;

    private readonly PokerSessionFaker _sessionFaker;
    private readonly EstimationScaleFaker _scaleFaker;

    public UpdatePokerSessionCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdatePokerSessionCommandHandler>>();
        _mockNotifier = new Mock<IPokerSessionNotifier>();

        _handler = new UpdatePokerSessionCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
        _sessionFaker = new PokerSessionFaker();
        _scaleFaker = new EstimationScaleFaker();
    }

    [Fact]
    public async Task Handle_ShouldUpdateNameAndScale_WhenNoRoundsExist()
    {
        // Arrange
        var originalScale = _scaleFaker.Generate();
        var newScale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(originalScale);
        _dbContext.AddEstimationScale(newScale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(originalScale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerSessionCommand(session.Id, "Updated Name", newScale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Name.Should().Be("Updated Name");
        session.EstimationScaleId.Should().Be(newScale.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySessionUpdated(session.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUpdateNameOnly_WhenRoundsExistAndScaleUnchanged()
    {
        // Arrange
        var scale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(scale)
            .Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Round 1");

        var command = new UpdatePokerSessionCommand(session.Id, "Updated Name", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        session.Name.Should().Be("Updated Name");
        session.EstimationScaleId.Should().Be(scale.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySessionUpdated(session.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenChangingScaleWithExistingRounds()
    {
        // Arrange
        var originalScale = _scaleFaker.Generate();
        var newScale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(originalScale);
        _dbContext.AddEstimationScale(newScale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(originalScale)
            .Generate();
        _dbContext.AddPokerSession(session);

        session.AddRound("Round 1");

        var command = new UpdatePokerSessionCommand(session.Id, "Updated Name", newScale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("scale");
        session.EstimationScaleId.Should().Be(originalScale.Id);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySessionUpdated(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSessionDoesNotExist()
    {
        // Arrange
        var command = new UpdatePokerSessionCommand(Guid.NewGuid(), "Name", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEstimationScaleDoesNotExist()
    {
        // Arrange
        var originalScale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(originalScale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(originalScale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerSessionCommand(session.Id, "Updated Name", Guid.NewGuid());

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
        var scale = _scaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Completed)
            .WithEstimationScale(scale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerSessionCommand(session.Id, "Updated Name", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySessionUpdated(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ChangingTheScale_ShouldPinTheNewScalesCurrentVersion()
    {
        // Arrange
        var originalScale = _scaleFaker.Generate();
        var newScale = _scaleFaker.WithValues("S", "M", "L").Generate();
        newScale.PublishVersion([new EstimationScaleValue("S", true), new EstimationScaleValue("M", true), new EstimationScaleValue("XL", true)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 0));
        _dbContext.AddEstimationScale(originalScale);
        _dbContext.AddEstimationScale(newScale);

        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(originalScale)
            .Generate();
        _dbContext.AddPokerSession(session);

        var command = new UpdatePokerSessionCommand(session.Id, session.Name, newScale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.EstimationScaleId.Should().Be(newScale.Id);
        session.EstimationScaleVersionId.Should().Be(newScale.Versions.Single(v => v.Number == 2).Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeepingTheScale_ShouldKeepThePinnedVersion()
    {
        // Arrange
        var scale = _scaleFaker.WithValues("S", "M", "L").Generate();
        _dbContext.AddEstimationScale(scale);
        var session = _sessionFaker
            .WithStatus(PokerSessionStatus.Active)
            .WithEstimationScale(scale)
            .Generate();
        _dbContext.AddPokerSession(session);
        var pinned = session.EstimationScaleVersionId;
        scale.PublishVersion([new EstimationScaleValue("S", true), new EstimationScaleValue("M", true), new EstimationScaleValue("XL", true)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 0));

        var command = new UpdatePokerSessionCommand(session.Id, "Renamed", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.Name.Should().Be("Renamed");
        session.EstimationScaleVersionId.Should().Be(pinned);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
