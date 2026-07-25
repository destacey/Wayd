using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class DeleteStepCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeleteStepCommandHandler _handler;
    private readonly Mock<ILogger<DeleteStepCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public DeleteStepCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeleteStepCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new DeleteStepCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldDeleteStep_WhenGoalKeepsAStep()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        var goalId = map.Goals[0].Id;
        // Add a second step so the goal survives the delete.
        map.AddStep(goalId, "Second step");
        var stepId = map.Goals[0].Steps.Single(s => s.Name == "Second step").Id;
        _dbContext.AddStoryMap(map);

        var command = new DeleteStepCommand(map.Id, stepId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals[0].Steps.Should().NotContain(s => s.Id == stepId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepDeleted(map.Id, stepId), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new DeleteStepCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepDeleted(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDeletingLastStepInGoal()
    {
        // Arrange — deleting a goal's only step is allowed; the goal is left with no steps.
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        var goal = map.Goals[0];
        var stepId = goal.Steps[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new DeleteStepCommand(map.Id, stepId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        goal.Steps.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepDeleted(map.Id, stepId), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
