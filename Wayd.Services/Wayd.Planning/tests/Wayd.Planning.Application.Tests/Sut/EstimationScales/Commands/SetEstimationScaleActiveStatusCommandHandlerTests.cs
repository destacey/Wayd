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

public class SetEstimationScaleActiveStatusCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetEstimationScaleActiveStatusCommandHandler _handler;
    private readonly Mock<ILogger<SetEstimationScaleActiveStatusCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));

    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30);

    public SetEstimationScaleActiveStatusCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetEstimationScaleActiveStatusCommandHandler>>();

        _handler = new SetEstimationScaleActiveStatusCommandHandler(_dbContext, _currentUser.Object, _dateTimeProvider, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldDeactivate_WhenOtherActiveScalesExist()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var otherScale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(otherScale);

        var command = new SetEstimationScaleActiveStatusCommand(scale.Id, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.IsActive.Should().BeFalse();
        var deactivated = scale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleDeactivatedEvent>().Subject;
        deactivated.Id.Should().Be(scale.Id);
        deactivated.Timestamp.Should().Be(Now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldActivate_WhenScaleIsInactive()
    {
        // Arrange
        var inactiveScale = new EstimationScaleFaker().WithIsActive(false).Generate();
        _dbContext.AddEstimationScale(inactiveScale);

        var command = new SetEstimationScaleActiveStatusCommand(inactiveScale.Id, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        inactiveScale.IsActive.Should().BeTrue();
        inactiveScale.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EstimationScaleActivatedEvent>()
            .Which.Id.Should().Be(inactiveScale.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenScaleDoesNotExist()
    {
        // Arrange
        var command = new SetEstimationScaleActiveStatusCommand(Guid.NewGuid(), false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDeactivatingLastActiveScale()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        var inactiveScale = new EstimationScaleFaker().WithIsActive(false).Generate();
        _dbContext.AddEstimationScale(scale);
        _dbContext.AddEstimationScale(inactiveScale);

        var command = new SetEstimationScaleActiveStatusCommand(scale.Id, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("last active");
        scale.IsActive.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAlreadyActive()
    {
        // Arrange
        var scale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(scale);

        var command = new SetEstimationScaleActiveStatusCommand(scale.Id, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already active");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAlreadyInactive()
    {
        // Arrange
        var inactiveScale = new EstimationScaleFaker().WithIsActive(false).Generate();
        var otherScale = new EstimationScaleFaker().Generate();
        _dbContext.AddEstimationScale(inactiveScale);
        _dbContext.AddEstimationScale(otherScale);

        var command = new SetEstimationScaleActiveStatusCommand(inactiveScale.Id, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already inactive");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
