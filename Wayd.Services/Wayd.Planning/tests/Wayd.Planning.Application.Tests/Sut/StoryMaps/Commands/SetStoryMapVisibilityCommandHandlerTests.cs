using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetStoryMapVisibilityCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IStoryMapNotifier> _mockNotifier = new();
    private readonly SetStoryMapVisibilityCommandHandler _handler;

    public SetStoryMapVisibilityCommandHandlerTests()
    {
        _handler = new SetStoryMapVisibilityCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetStoryMapVisibilityCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldMakeAnArchivedMapPrivateAndRecheckAccess()
    {
        // Arrange
        var map = new StoryMapFaker().WithStatus(StatusCategory.Removed).Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(new SetStoryMapVisibilityCommand(map.Id, Visibility.Private), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        map.Visibility.Should().Be(Visibility.Private);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(map.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ByViewer_ShouldFailAsNotAnOwner()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);
        _access.Actor = StoryMapActors.Viewer;

        // Act
        var result = await _handler.Handle(new SetStoryMapVisibilityCommand(map.Id, Visibility.Private), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError);
        map.Visibility.Should().Be(Visibility.Public);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
