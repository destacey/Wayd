using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.EstimationScales.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Commands;

public class CreateEstimationScaleCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30);

    private readonly FakePlanningDbContext _dbContext;
    private readonly CreateEstimationScaleCommandHandler _handler;
    private readonly Mock<ILogger<CreateEstimationScaleCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));

    public CreateEstimationScaleCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<CreateEstimationScaleCommandHandler>>();

        _handler = new CreateEstimationScaleCommandHandler(_dbContext, _currentUser.Object, _dateTimeProvider, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateScaleWithVersionOne_WhenValidCommand()
    {
        // Arrange
        var command = new CreateEstimationScaleCommand(
            "Fibonacci",
            "Fibonacci sequence for estimation",
            [new("1", true), new("2", true), new("3", true), new("?", false)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var scale = _dbContext.EstimationScales.Should().ContainSingle().Subject;
        result.Value.Id.Should().Be(scale.Id);
        result.Value.Key.Should().Be(scale.Key);
        scale.Name.Should().Be("Fibonacci");
        scale.CurrentVersion.Number.Should().Be(1);
        scale.CurrentVersion.PublishedOn.Should().Be(Now);
        scale.CurrentVersion.Values.Select(v => (v.Value, v.IsEstimate))
            .Should().Equal(("1", true), ("2", true), ("3", true), ("?", false));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateScale_WhenNoDescription()
    {
        // Arrange
        var command = new CreateEstimationScaleCommand(
            "Custom Scale",
            null,
            [new("S", true), new("M", true), new("L", true)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.EstimationScales.Should().ContainSingle().Which.Description.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenFewerThanTwoEstimates()
    {
        // Arrange
        var command = new CreateEstimationScaleCommand(
            "Bad Scale",
            null,
            [new("1", true), new("?", false), new("☕", false)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An estimation scale must have at least 2 estimate values.");
        _dbContext.EstimationScales.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
