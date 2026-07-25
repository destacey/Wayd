using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ReorderStepCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly ReorderStepCommandHandler _handler;
    private readonly Mock<ILogger<ReorderStepCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ReorderStepCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ReorderStepCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ReorderStepCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReorderStep_WhenStepExists()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        var goalId = map.Goals[0].Id;
        map.AddStep(goalId, "Second step");
        var secondStepId = map.Goals[0].Steps.Single(s => s.Name == "Second step").Id;
        _dbContext.AddStoryMap(map);

        var command = new ReorderStepCommand(map.Id, secondStepId, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals[0].Steps[0].Id.Should().Be(secondStepId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepReordered(map.Id, secondStepId, 0), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new ReorderStepCommand(Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepReordered(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStepDoesNotExist()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new ReorderStepCommand(map.Id, Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepReordered(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
