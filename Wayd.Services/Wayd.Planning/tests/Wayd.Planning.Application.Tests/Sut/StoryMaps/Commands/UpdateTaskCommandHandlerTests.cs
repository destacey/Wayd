using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class UpdateTaskCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdateTaskCommandHandler _handler;
    private readonly Mock<ILogger<UpdateTaskCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public UpdateTaskCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdateTaskCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new UpdateTaskCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static (StoryMap Map, Guid TaskId) CreateMapWithTask()
    {
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var task = map.AddTask(stepId, "T").Value;
        return (map, task.Id);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTask_WhenMapExists()
    {
        // Arrange
        var (map, taskId) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new UpdateTaskCommand(map.Id, taskId, "Updated title", "Some description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == taskId);
        task.Title.Should().Be("Updated title");
        task.Description.Should().Be("Some description");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskUpdated(map.Id, It.IsAny<StoryMapTaskDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new UpdateTaskCommand(Guid.NewGuid(), Guid.NewGuid(), "Updated title", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskUpdated(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var (map, _) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new UpdateTaskCommand(map.Id, Guid.NewGuid(), "Updated title", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskUpdated(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
