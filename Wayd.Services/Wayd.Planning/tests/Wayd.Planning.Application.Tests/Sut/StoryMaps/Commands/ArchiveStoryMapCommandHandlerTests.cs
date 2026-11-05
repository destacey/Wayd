using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ArchiveStoryMapCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly ArchiveStoryMapCommandHandler _handler;
    private readonly Mock<ILogger<ArchiveStoryMapCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ArchiveStoryMapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ArchiveStoryMapCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ArchiveStoryMapCommandHandler(_dbContext, _access, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", null, "Goal", "Step");

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
        map.Status.Should().Be(StatusCategory.Removed);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyMapArchived(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ByEditor_ShouldFailAsNotAnOwner()
    {
        // Arrange — the hole this closes: anyone who could edit could archive any map.
        var map = CreateMap();
        _dbContext.AddStoryMap(map);
        _access.Actor = StoryMapActors.Viewer;

        // Act
        var result = await _handler.Handle(new ArchiveStoryMapCommand(map.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError);
        map.Status.Should().Be(StatusCategory.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapAlreadyArchived()
    {
        // Arrange
        var map = CreateMap();
        map.Archive(StoryMapActors.Owner);
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
