using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetTaskPersonasCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly SetTaskPersonasCommandHandler _handler;
    private readonly Mock<ILogger<SetTaskPersonasCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public SetTaskPersonasCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<SetTaskPersonasCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new SetTaskPersonasCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static (StoryMap Map, Guid TaskId, Guid PersonaId) CreateMapWithTaskAndPersona()
    {
        var map = StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");
        var stepId = map.Goals[0].Steps[0].Id;
        var task = map.AddTask(stepId, "T").Value;
        var personaId = map.AddPersona("P", null, "#FFFFFF").Value.Id;
        return (map, task.Id, personaId);
    }

    [Fact]
    public async Task Handle_ShouldSetPersonas_WhenMapExists()
    {
        // Arrange
        var (map, taskId, personaId) = CreateMapWithTaskAndPersona();
        _dbContext.AddStoryMap(map);

        var personaIds = new List<Guid> { personaId };
        var command = new SetTaskPersonasCommand(map.Id, taskId, personaIds);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = map.Goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).First(t => t.Id == taskId);
        task.PersonaIds.Should().Contain(personaId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyTaskPersonasChanged(map.Id, taskId, personaIds), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new SetTaskPersonasCommand(Guid.NewGuid(), Guid.NewGuid(), new List<Guid>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskPersonasChanged(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTaskDoesNotExist()
    {
        // Arrange
        var (map, _, personaId) = CreateMapWithTaskAndPersona();
        _dbContext.AddStoryMap(map);

        var command = new SetTaskPersonasCommand(map.Id, Guid.NewGuid(), new List<Guid> { personaId });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskPersonasChanged(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPersonaIdIsUnknown()
    {
        // Arrange
        var (map, taskId, _) = CreateMapWithTaskAndPersona();
        _dbContext.AddStoryMap(map);

        var command = new SetTaskPersonasCommand(map.Id, taskId, new List<Guid> { Guid.NewGuid() });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyTaskPersonasChanged(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
