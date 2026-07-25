using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Common.Domain.Identity;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.TestData.Core;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ChangeStoryMapOwnerCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly ChangeStoryMapOwnerCommandHandler _handler;
    private readonly Mock<ILogger<ChangeStoryMapOwnerCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ChangeStoryMapOwnerCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ChangeStoryMapOwnerCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ChangeStoryMapOwnerCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static User CreateUser(string id) =>
        new PrivateConstructorFaker<User>().RuleFor(u => u.Id, id).Generate();

    [Fact]
    public async Task Handle_ShouldChangeOwner_WhenOwnerExistsAndMapFound()
    {
        // Arrange
        var newOwnerId = Guid.NewGuid().ToString();
        _dbContext.AddUser(CreateUser(newOwnerId));

        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new ChangeStoryMapOwnerCommand(map.Id, newOwnerId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.OwnerId.Should().Be(newOwnerId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMapUpdated(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOwnerDoesNotExist()
    {
        // Arrange
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        _dbContext.AddStoryMap(map);

        var command = new ChangeStoryMapOwnerCommand(map.Id, Guid.NewGuid().ToString());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyMapUpdated(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var newOwnerId = Guid.NewGuid().ToString();
        _dbContext.AddUser(CreateUser(newOwnerId));

        var command = new ChangeStoryMapOwnerCommand(Guid.NewGuid(), newOwnerId);

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
