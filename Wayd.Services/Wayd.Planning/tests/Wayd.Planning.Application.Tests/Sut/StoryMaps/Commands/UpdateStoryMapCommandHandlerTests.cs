using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class UpdateStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdateStoryMapCommandHandler _handler;
    private readonly Mock<ILogger<UpdateStoryMapCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public UpdateStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdateStoryMapCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new UpdateStoryMapCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldUpdateNameAndDescription_WhenMapExists()
    {
        // Arrange
        var map = StoryMap.Create("Original", "Original description", Guid.NewGuid().ToString(), "Goal", "Step").Value;
        _dbContext.AddStoryMap(map);

        var command = new UpdateStoryMapCommand(map.Id, "Updated name", "Updated description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Name.Should().Be("Updated name");
        map.Description.Should().Be("Updated description");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMapUpdated(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new UpdateStoryMapCommand(Guid.NewGuid(), "Updated name", "Updated description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyMapUpdated(It.IsAny<Guid>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
