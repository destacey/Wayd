using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class AddChecklistItemCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly AddChecklistItemCommandHandler _handler;
    private readonly Mock<ILogger<AddChecklistItemCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public AddChecklistItemCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<AddChecklistItemCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new AddChecklistItemCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldAddChecklistItemAndNotify_WhenTaskExists()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var task = map.AddTask(map.Goals[0].Steps[0].Id, "T").Value;
        _dbContext.AddStoryMap(map);

        var command = new AddChecklistItemCommand(map.Id, task.Id, "item");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // ChecklistTotalCount is a custom Mapster mapping (from CompletionCount.Total) registered
        // only in production via AddPlanningApplication; at the unit level the default map populates
        // the Checklist collection, so assert the item landed there.
        result.Value.Checklist.Should().ContainSingle(i => i.Name == "item");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskChecklistChanged(map.Id, It.IsAny<StoryMapTaskDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new AddChecklistItemCommand(Guid.NewGuid(), Guid.NewGuid(), "item");

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

        var command = new AddChecklistItemCommand(map.Id, Guid.NewGuid(), "item");

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
