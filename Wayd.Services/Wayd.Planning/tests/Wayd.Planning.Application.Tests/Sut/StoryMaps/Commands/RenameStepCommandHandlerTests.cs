using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class RenameStepCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly RenameStepCommandHandler _handler;
    private readonly Mock<ILogger<RenameStepCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public RenameStepCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<RenameStepCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new RenameStepCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldRenameStep_WhenStepExists()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new RenameStepCommand(map.Id, stepId, "Renamed step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals[0].Steps[0].Name.Should().Be("Renamed step");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyStepRenamed(map.Id, stepId, "Renamed step"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new RenameStepCommand(Guid.NewGuid(), Guid.NewGuid(), "Renamed step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepRenamed(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenStepDoesNotExist()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Description", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new RenameStepCommand(map.Id, Guid.NewGuid(), "Renamed step");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyStepRenamed(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
