using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class RenameSwimLaneCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly RenameSwimLaneCommandHandler _handler;
    private readonly Mock<ILogger<RenameSwimLaneCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public RenameSwimLaneCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<RenameSwimLaneCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new RenameSwimLaneCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldRenameLane_WhenLaneIsNotDefault()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;
        _dbContext.AddStoryMap(map);

        var command = new RenameSwimLaneCommand(map.Id, lane.Id, "Release 2");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lane.Name.Should().Be("Release 2");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySwimLaneRenamed(map.Id, lane.Id, "Release 2"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaneIsDefault()
    {
        // Arrange
        var map = CreateMap();
        var defaultSwimLaneId = map.SwimLanes.Single(l => l.IsDefault).Id;
        _dbContext.AddStoryMap(map);

        var command = new RenameSwimLaneCommand(map.Id, defaultSwimLaneId, "Renamed");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneRenamed(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new RenameSwimLaneCommand(Guid.NewGuid(), Guid.NewGuid(), "Release 2");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneRenamed(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
