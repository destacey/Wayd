using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class CreatePokerSessionCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly CreatePokerSessionCommandHandler _handler;
    private readonly Mock<ILogger<CreatePokerSessionCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;

    private readonly EstimationScaleFaker _estimationScaleFaker;
    private readonly string _currentUserId = Guid.NewGuid().ToString();

    public CreatePokerSessionCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<CreatePokerSessionCommandHandler>>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_currentUserId);
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockDateTimeProvider.Setup(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 10, 0));

        _handler = new CreatePokerSessionCommandHandler(_dbContext, _mockCurrentUser.Object, _mockDateTimeProvider.Object, _mockLogger.Object);
        _estimationScaleFaker = new EstimationScaleFaker();
    }

    [Fact]
    public async Task Handle_ShouldCreatePokerSession_WhenValidCommand()
    {
        // Arrange
        var scale = _estimationScaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new CreatePokerSessionCommand("Sprint Planning", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEstimationScaleDoesNotExist()
    {
        // Arrange
        var command = new CreatePokerSessionCommand("Sprint Planning", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Estimation scale not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEstimationScaleIsInactive()
    {
        // Arrange
        var scale = _estimationScaleFaker.WithIsActive(false).Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new CreatePokerSessionCommand("Sprint Planning", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The estimation scale is inactive and cannot be chosen for a session.");
        _dbContext.PokerSessions.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSetFacilitatorToCurrentUser()
    {
        // Arrange
        var scale = _estimationScaleFaker.Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new CreatePokerSessionCommand("Sprint Planning", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockCurrentUser.Verify(u => u.GetUserId(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPinTheScalesCurrentVersion()
    {
        // Arrange
        var scale = _estimationScaleFaker.WithValues("S", "M", "L").Generate();
        scale.PublishVersion([new EstimationScaleValue("S", true), new EstimationScaleValue("M", true), new EstimationScaleValue("XL", true)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 0));
        _dbContext.AddEstimationScale(scale);

        var command = new CreatePokerSessionCommand("Sprint Planning", scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var session = _dbContext.PokerSessions.Should().ContainSingle().Subject;
        session.EstimationScaleId.Should().Be(scale.Id);
        session.EstimationScaleVersionId.Should().Be(scale.Versions.Single(v => v.Number == 2).Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
