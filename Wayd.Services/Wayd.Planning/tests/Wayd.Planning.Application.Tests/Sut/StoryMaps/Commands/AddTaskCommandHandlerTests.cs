using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class AddTaskCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly AddTaskCommandHandler _handler;
    private readonly Mock<ILogger<AddTaskCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public AddTaskCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<AddTaskCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new AddTaskCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldAddTask_WhenMapExists()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals[0].Steps[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new AddTaskCommand(map.Id, stepId, "New task", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New task");
        map.Goals[0].Steps[0].Tasks.Should().ContainSingle(t => t.Title == "New task");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskAdded(map.Id, It.IsAny<StoryMapTaskDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new AddTaskCommand(Guid.NewGuid(), Guid.NewGuid(), "New task", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskAdded(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStepDoesNotExist()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new AddTaskCommand(map.Id, Guid.NewGuid(), "New task", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskAdded(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
