using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetGoalPersonasCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetGoalPersonasCommandHandler _handler;
    private readonly Mock<ILogger<SetGoalPersonasCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public SetGoalPersonasCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetGoalPersonasCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new SetGoalPersonasCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldSetGoalPersonas_WhenPersonaExists()
    {
        // Arrange
        var map = CreateMap();
        var p = map.AddPersona("Field tech", null, "#4096FF").Value;
        var goalId = map.Goals[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new SetGoalPersonasCommand(map.Id, goalId, new[] { p.Id });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Goals.First(g => g.Id == goalId).PersonaIds.Should().Contain(p.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyGoalPersonasChanged(map.Id, goalId, It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPersonaUnknown()
    {
        // Arrange
        var map = CreateMap();
        var goalId = map.Goals[0].Id;
        _dbContext.AddStoryMap(map);

        var command = new SetGoalPersonasCommand(map.Id, goalId, new[] { Guid.NewGuid() });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyGoalPersonasChanged(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new SetGoalPersonasCommand(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyGoalPersonasChanged(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
