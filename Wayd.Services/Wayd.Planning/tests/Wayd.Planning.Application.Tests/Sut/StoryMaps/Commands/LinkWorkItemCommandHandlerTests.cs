using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class LinkWorkItemCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly LinkWorkItemCommandHandler _handler;
    private readonly Mock<ILogger<LinkWorkItemCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public LinkWorkItemCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<LinkWorkItemCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new LinkWorkItemCommandHandler(_dbContext, _access, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldLinkWorkItem_WhenTaskExists()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", null, "Goal", "Step");
        var task = map.AddTask(map.Goals[0].Steps[0].Id, "T", null, StoryMapActors.Owner).Value;
        _dbContext.AddStoryMap(map);

        var command = new LinkWorkItemCommand(map.Id, task.Id, 123);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskWorkItemLinked(map.Id, task.Id, 123), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWorkItemAlreadyLinkedToAnotherTask()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", null, "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var task1 = map.AddTask(stepId, "T1", null, StoryMapActors.Owner).Value;
        var task2 = map.AddTask(stepId, "T2", null, StoryMapActors.Owner).Value;
        map.LinkWorkItem(task1.Id, 123, StoryMapActors.Owner);
        _dbContext.AddStoryMap(map);

        var command = new LinkWorkItemCommand(map.Id, task2.Id, 123);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already linked");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskWorkItemLinked(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new LinkWorkItemCommand(Guid.NewGuid(), Guid.NewGuid(), 123);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskWorkItemLinked(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
