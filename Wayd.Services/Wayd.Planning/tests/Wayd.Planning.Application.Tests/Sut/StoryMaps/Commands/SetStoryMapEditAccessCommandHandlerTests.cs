using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetStoryMapEditAccessCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IStoryMapNotifier> _mockNotifier = new();
    private readonly SetStoryMapEditAccessCommandHandler _handler;

    public SetStoryMapEditAccessCommandHandlerTests()
    {
        _handler = new SetStoryMapEditAccessCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetStoryMapEditAccessCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldRestrictEditingAndTellViewers()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(new SetStoryMapEditAccessCommand(map.Id, StoryMapEditAccess.OwnersAndEditors), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        map.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(map.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ByEditor_ShouldFailAsNotAnOwner()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var map = new StoryMapFaker().WithEditors(editorId).Generate();
        _dbContext.AddStoryMap(map);
        _access.Actor = StoryMapActors.Employee(editorId);

        // Act
        var result = await _handler.Handle(new SetStoryMapEditAccessCommand(map.Id, StoryMapEditAccess.OwnersAndEditors), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError);
        map.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
