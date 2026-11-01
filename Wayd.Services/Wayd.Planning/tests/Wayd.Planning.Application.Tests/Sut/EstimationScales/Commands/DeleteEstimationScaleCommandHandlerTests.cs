using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.EstimationScales.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Commands;

public class DeleteEstimationScaleCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeleteEstimationScaleCommandHandler _handler;
    private readonly Mock<ILogger<DeleteEstimationScaleCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));

    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30);

    public DeleteEstimationScaleCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeleteEstimationScaleCommandHandler>>();

        _handler = new DeleteEstimationScaleCommandHandler(_dbContext, _currentUser.Object, _dateTimeProvider, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldDeleteScale_WhenScaleExistsAndNotInUse()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var otherScale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(otherScale);

        var command = new DeleteEstimationScaleCommand(scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.EstimationScales.Should().NotContain(scale);
        var deleted = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDeletedEvent>().Subject;
        deleted.Id.Should().Be(scale.Id);
        deleted.Name.Should().Be(scale.Name);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDeleteInactiveScale_WhenActiveScalesExist()
    {
        // Arrange
        var inactiveScale = new EstimationScaleFaker().WithIsActive(false).Generate();
        var activeScale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(inactiveScale);
        _dbContext.AddEstimationScale(activeScale);

        var command = new DeleteEstimationScaleCommand(inactiveScale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenScaleDoesNotExist()
    {
        // Arrange
        var command = new DeleteEstimationScaleCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenScaleIsInUseByPokerSession()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var otherScale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(otherScale);

        var session = new PokerSessionFaker().WithEstimationScale(scale).Generate();
        _dbContext.AddPokerSession(session);

        var command = new DeleteEstimationScaleCommand(scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("in use");
        scale.DomainEvents.Should().BeEmpty();
        _dbContext.EstimationScales.Should().Contain(scale);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDeletingLastActiveScale()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var inactiveScale = new EstimationScaleFaker().WithIsActive(false).Generate();
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(inactiveScale);

        var command = new DeleteEstimationScaleCommand(scale.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("last active");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
