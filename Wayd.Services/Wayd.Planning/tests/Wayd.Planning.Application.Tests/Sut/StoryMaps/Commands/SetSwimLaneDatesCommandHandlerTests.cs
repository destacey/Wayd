using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetSwimLaneDatesCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetSwimLaneDatesCommandHandler _handler;
    private readonly Mock<ILogger<SetSwimLaneDatesCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public SetSwimLaneDatesCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetSwimLaneDatesCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new SetSwimLaneDatesCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldSetLaneDates_WhenLaneExists()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1").Value;
        _dbContext.AddStoryMap(map);

        var startDate = new LocalDate(2026, 1, 1);
        var endDate = new LocalDate(2026, 3, 31);
        var command = new SetSwimLaneDatesCommand(map.Id, lane.Id, startDate, endDate);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lane.StartDate.Should().Be(startDate);
        lane.EndDate.Should().Be(endDate);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifySwimLaneDatesChanged(map.Id, It.IsAny<StoryMapSwimLaneDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new SetSwimLaneDatesCommand(Guid.NewGuid(), Guid.NewGuid(), null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneDatesChanged(It.IsAny<Guid>(), It.IsAny<StoryMapSwimLaneDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLaneDoesNotExist()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new SetSwimLaneDatesCommand(map.Id, Guid.NewGuid(), null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifySwimLaneDatesChanged(It.IsAny<Guid>(), It.IsAny<StoryMapSwimLaneDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
