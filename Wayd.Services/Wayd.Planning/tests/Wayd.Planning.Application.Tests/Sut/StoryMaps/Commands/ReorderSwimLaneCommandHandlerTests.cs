using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ReorderSwimLaneCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly ReorderSwimLaneCommandHandler _handler;
    private readonly Mock<ILogger<ReorderSwimLaneCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ReorderSwimLaneCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ReorderSwimLaneCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ReorderSwimLaneCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldReorderLane_WhenLaneIsNotDefault()
    {
        // Arrange
        var map = CreateMap();
        map.AddSwimLane("Release 1");
        var lane = map.AddSwimLane("Release 2").Value;
        _dbContext.AddStoryMap(map);

        var command = new ReorderSwimLaneCommand(map.Id, lane.Id, 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySwimLaneReordered(map.Id, lane.Id, 1), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaneIsDefault()
    {
        // Arrange
        var map = CreateMap();
        var defaultSwimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        _dbContext.AddStoryMap(map);

        var command = new ReorderSwimLaneCommand(map.Id, defaultSwimLaneId, 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneReordered(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new ReorderSwimLaneCommand(Guid.NewGuid(), Guid.NewGuid(), 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneReordered(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
