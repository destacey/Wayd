using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class DeleteGoalCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeleteGoalCommandHandler _handler;
    private readonly Mock<ILogger<DeleteGoalCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public DeleteGoalCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeleteGoalCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new DeleteGoalCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldDeleteGoal_WhenMapHasMoreThanOneGoal()
    {
        // Arrange
        var map = CreateMap();
        var secondGoal = map.AddGoal("Second goal").Value;
        _dbContext.AddStoryMap(map);

        var command = new DeleteGoalCommand(map.Id, secondGoal.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Should().NotContain(g => g.Id == secondGoal.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyGoalDeleted(map.Id, secondGoal.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new DeleteGoalCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyGoalDeleted(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDeletingLastGoal()
    {
        // Arrange — deleting the last goal is allowed; the map returns to its empty state.
        var map = CreateMap();
        var onlyGoal = map.Goals[0];
        _dbContext.AddStoryMap(map);

        var command = new DeleteGoalCommand(map.Id, onlyGoal.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyGoalDeleted(map.Id, onlyGoal.Id), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
