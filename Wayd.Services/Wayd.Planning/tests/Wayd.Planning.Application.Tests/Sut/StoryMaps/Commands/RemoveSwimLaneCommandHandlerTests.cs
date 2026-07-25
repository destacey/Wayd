using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class RemoveSwimLaneCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly RemoveSwimLaneCommandHandler _handler;
    private readonly Mock<ILogger<RemoveSwimLaneCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public RemoveSwimLaneCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<RemoveSwimLaneCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new RemoveSwimLaneCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldRemoveLane_WhenLaneIsNotDefault()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;
        var defaultSwimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        _dbContext.AddStoryMap(map);

        var command = new RemoveSwimLaneCommand(map.Id, lane.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        map.SwimLanes.Should().NotContain(l => l.Id == lane.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySwimLaneRemoved(map.Id, lane.Id, defaultSwimLaneId, 0), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaneIsDefault()
    {
        // Arrange
        var map = CreateMap();
        var defaultSwimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        _dbContext.AddStoryMap(map);

        var command = new RemoveSwimLaneCommand(map.Id, defaultSwimLaneId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneRemoved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new RemoveSwimLaneCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneRemoved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
