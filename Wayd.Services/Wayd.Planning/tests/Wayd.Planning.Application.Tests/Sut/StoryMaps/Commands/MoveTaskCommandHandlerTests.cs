using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class MoveTaskCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly MoveTaskCommandHandler _handler;
    private readonly Mock<ILogger<MoveTaskCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public MoveTaskCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<MoveTaskCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new MoveTaskCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static (StoryMap Map, Guid TaskId, Guid StepId, Guid SwimLaneId) CreateMapWithTask()
    {
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var swimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        var task = map.AddTask(stepId, "T").Value;
        return (map, task.Id, stepId, swimLaneId);
    }

    [Fact]
    public async Task Handle_ShouldMoveTask_WhenMapExists()
    {
        // Arrange
        var (map, taskId, stepId, swimLaneId) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new MoveTaskCommand(map.Id, taskId, stepId, swimLaneId, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskMoved(map.Id, taskId, stepId, swimLaneId, 0), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new MoveTaskCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskMoved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var (map, _, stepId, swimLaneId) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new MoveTaskCommand(map.Id, Guid.NewGuid(), stepId, swimLaneId, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskMoved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
