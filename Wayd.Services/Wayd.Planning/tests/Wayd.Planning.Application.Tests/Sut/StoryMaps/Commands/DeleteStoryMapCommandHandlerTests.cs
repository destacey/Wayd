using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class DeleteStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeleteStoryMapCommandHandler _handler;
    private readonly Mock<ILogger<DeleteStoryMapCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public DeleteStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<DeleteStoryMapCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new DeleteStoryMapCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldRemoveAndSave_WhenMapExists()
    {
        // Arrange
        var map = StoryMap.Create("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step").Value;
        _dbContext.AddStoryMap(map);

        var command = new DeleteStoryMapCommand(map.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.StoryMaps.Should().NotContain(map);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMapDeleted(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new DeleteStoryMapCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyMapDeleted(It.IsAny<Guid>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
