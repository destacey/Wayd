using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetTaskDescriptionCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetTaskDescriptionCommandHandler _handler;
    private readonly Mock<ILogger<SetTaskDescriptionCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public SetTaskDescriptionCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetTaskDescriptionCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new SetTaskDescriptionCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static (StoryMap Map, Guid TaskId) CreateMapWithTask()
    {
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var task = map.AddTask(stepId, "T").Value;
        return (map, task.Id);
    }

    [Fact]
    public async Task Handle_ShouldSetDescription_WhenMapExists()
    {
        // Arrange
        var (map, taskId) = CreateMapWithTask();
        _dbContext.AddStoryMap(map);

        var command = new SetTaskDescriptionCommand(map.Id, taskId, "Some notes");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == taskId);
        task.Description.Should().Be("Some notes");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskUpdated(map.Id, It.IsAny<StoryMapTaskDto>()), Times.Once);
    }

    /// <summary>
    /// The reason this command exists: setting notes must not disturb a rename made elsewhere.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLeaveTitleUntouched()
    {
        // Arrange
        var (map, taskId) = CreateMapWithTask();
        map.RenameTask(taskId, "Renamed on the card");
        _dbContext.AddStoryMap(map);

        var command = new SetTaskDescriptionCommand(map.Id, taskId, "Some notes");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == taskId);
        task.Title.Should().Be("Renamed on the card");
        task.Description.Should().Be("Some notes");
    }

    [Fact]
    public async Task Handle_ShouldClearDescription_WhenNull()
    {
        // Arrange
        var (map, taskId) = CreateMapWithTask();
        map.SetTaskDescription(taskId, "Existing notes");
        _dbContext.AddStoryMap(map);

        var command = new SetTaskDescriptionCommand(map.Id, taskId, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == taskId);
        task.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new SetTaskDescriptionCommand(Guid.NewGuid(), Guid.NewGuid(), "Some notes");

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

        var command = new SetTaskDescriptionCommand(map.Id, Guid.NewGuid(), "Some notes");

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
