using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class AddSwimLaneCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly AddSwimLaneCommandHandler _handler;
    private readonly Mock<ILogger<AddSwimLaneCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public AddSwimLaneCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<AddSwimLaneCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new AddSwimLaneCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldAddLane_WhenMapExists()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new AddSwimLaneCommand(map.Id, "Release 1");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Release 1");
        result.Value.IsDefault.Should().BeFalse();
        map.SwimLanes.Should().Contain(l => l.Name == "Release 1");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySwimLaneAdded(map.Id, It.IsAny<StoryMapSwimLaneDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new AddSwimLaneCommand(Guid.NewGuid(), "Release 1");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneAdded(It.IsAny<Guid>(), It.IsAny<StoryMapSwimLaneDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
