using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class CreateStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly CreateStoryMapCommandHandler _handler;
    private readonly Mock<ILogger<CreateStoryMapCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _mockCurrentUser;

    private readonly string _currentUserId = Guid.NewGuid().ToString();

    public CreateStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<CreateStoryMapCommandHandler>>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetUserId()).Returns(_currentUserId);

        _handler = new CreateStoryMapCommandHandler(_dbContext, _mockCurrentUser.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateStoryMap_WhenValidCommand()
    {
        // Arrange
        var command = new CreateStoryMapCommand("Checkout redesign", "A description");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSetOwnerToCurrentUser()
    {
        // Arrange
        var command = new CreateStoryMapCommand("Checkout redesign", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _dbContext.StoryMaps.Single();
        created.OwnerId.Should().Be(_currentUserId);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
