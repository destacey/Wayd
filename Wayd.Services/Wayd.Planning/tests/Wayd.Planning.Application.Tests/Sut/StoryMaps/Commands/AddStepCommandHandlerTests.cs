using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class AddStepCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly AddStepCommandHandler _handler;
    private readonly Mock<ILogger<AddStepCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public AddStepCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<AddStepCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new AddStepCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldAddStep_WhenMapAndGoalExist()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        var goalId = map.Goals[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new AddStepCommand(map.Id, goalId, "New step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New step");
        result.Value.GoalId.Should().Be(goalId);
        map.Goals[0].Steps.Should().Contain(s => s.Name == "New step");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepAdded(map.Id, It.IsAny<StoryMapStepDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new AddStepCommand(Guid.NewGuid(), Guid.NewGuid(), "New step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepAdded(It.IsAny<Guid>(), It.IsAny<StoryMapStepDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenGoalDoesNotExist()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new AddStepCommand(map.Id, Guid.NewGuid(), "New step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepAdded(It.IsAny<Guid>(), It.IsAny<StoryMapStepDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
