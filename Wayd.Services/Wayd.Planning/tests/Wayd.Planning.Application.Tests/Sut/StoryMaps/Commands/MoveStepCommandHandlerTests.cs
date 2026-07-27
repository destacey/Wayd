using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class MoveStepCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly MoveStepCommandHandler _handler;
    private readonly Mock<ILogger<MoveStepCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public MoveStepCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<MoveStepCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new MoveStepCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldMoveStep_WhenSourceGoalKeepsAStep()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal 1", "Step");
        map.AddGoal("Goal 2");
        var sourceGoalId = map.Goals[0].Id;
        var targetGoalId = map.Goals[1].Id;
        // Give the source goal a second step so it can survive the move.
        map.AddStep(sourceGoalId, "Movable step");
        var stepId = map.Goals[0].Steps.Single(s => s.Name == "Movable step").Id;
        _dbContext.AddStoryMap(map);

        var command = new MoveStepCommand(map.Id, stepId, targetGoalId, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single(g => g.Id == targetGoalId).Steps.Should().Contain(s => s.Id == stepId);
        map.Goals.Single(g => g.Id == sourceGoalId).Steps.Should().NotContain(s => s.Id == stepId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepMoved(map.Id, stepId, targetGoalId, 0), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new MoveStepCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepMoved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenMovingLastStepOutOfGoal()
    {
        // Arrange — moving a goal's only step is allowed; the source goal is left with no steps.
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal 1", "Step");
        map.AddGoal("Goal 2");
        var sourceGoalId = map.Goals[0].Id;
        var targetGoalId = map.Goals[1].Id;
        var stepId = map.Goals.Single(g => g.Id == sourceGoalId).Steps[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new MoveStepCommand(map.Id, stepId, targetGoalId, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Single(g => g.Id == sourceGoalId).Steps.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepMoved(map.Id, stepId, targetGoalId, 0), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
