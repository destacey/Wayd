using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class RemoveChecklistItemCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly RemoveChecklistItemCommandHandler _handler;
    private readonly Mock<ILogger<RemoveChecklistItemCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public RemoveChecklistItemCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<RemoveChecklistItemCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new RemoveChecklistItemCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldRemoveAndNotify_WhenItemExists()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var task = map.AddTask(map.Goals[0].Steps[0].Id, "T").Value;
        var item = map.AddChecklistItem(task.Id, "item").Value;
        _dbContext.AddStoryMap(map);

        var command = new RemoveChecklistItemCommand(map.Id, task.Id, item.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskChecklistChanged(map.Id, It.IsAny<StoryMapTaskDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new RemoveChecklistItemCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskChecklistChanged(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskNotFound()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new RemoveChecklistItemCommand(map.Id, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskChecklistChanged(It.IsAny<Guid>(), It.IsAny<StoryMapTaskDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
