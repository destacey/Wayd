using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class DeleteTaskCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeleteTaskCommandHandler _handler;
    private readonly Mock<ILogger<DeleteTaskCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public DeleteTaskCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeleteTaskCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new DeleteTaskCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static (StoryMap Map, Guid TaskId) CreateMapWithTask()
    {
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var task = map.AddTask(stepId, "T").Value;
        return (map, task.Id);
    }

    [Fact]
    public async Task Handle_ShouldDeleteTask_WhenMapExists()
    {
        // Arrange
        var (map, taskId) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new DeleteTaskCommand(map.Id, taskId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).Should().NotContain(t => t.Id == taskId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskDeleted(map.Id, taskId), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new DeleteTaskCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskDeleted(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var (map, _) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new DeleteTaskCommand(map.Id, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskDeleted(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
