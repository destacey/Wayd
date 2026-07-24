using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ArchiveStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly ArchiveStoryMapCommandHandler _handler;
    private readonly Mock<ILogger<ArchiveStoryMapCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ArchiveStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ArchiveStoryMapCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ArchiveStoryMapCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMap.Create("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step").Value;

    [Fact]
    public async Task Handle_ShouldArchive_WhenMapIsActive()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new ArchiveStoryMapCommand(map.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Status.Should().Be(WorkStatusCategory.Removed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMapArchived(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapAlreadyArchived()
    {
        // Arrange
        var map = CreateMap();
        map.Archive();
        _dbContext.AddStoryMap(map);

        var command = new ArchiveStoryMapCommand(map.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyMapArchived(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new ArchiveStoryMapCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyMapArchived(It.IsAny<Guid>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
