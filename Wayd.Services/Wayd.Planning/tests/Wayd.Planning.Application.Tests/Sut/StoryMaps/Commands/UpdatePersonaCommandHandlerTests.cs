using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class UpdatePersonaCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdatePersonaCommandHandler _handler;
    private readonly Mock<ILogger<UpdatePersonaCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public UpdatePersonaCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdatePersonaCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new UpdatePersonaCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldUpdatePersona_WhenMapAndPersonaExist()
    {
        // Arrange
        var map = CreateMap();
        var p = map.AddPersona("Field tech", null, "#4096FF").Value;
        _dbContext.AddStoryMap(map);

        var command = new UpdatePersonaCommand(map.Id, p.Id, "Support agent", "Desk-based", "#FF4D4F");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Personas.First(x => x.Id == p.Id).Name.Should().Be("Support agent");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyPersonaUpdated(map.Id, It.IsAny<StoryMapPersonaDto>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPersonaNotFound()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new UpdatePersonaCommand(map.Id, Guid.NewGuid(), "Support agent", null, "#FF4D4F");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyPersonaUpdated(It.IsAny<Guid>(), It.IsAny<StoryMapPersonaDto>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new UpdatePersonaCommand(Guid.NewGuid(), Guid.NewGuid(), "Support agent", null, "#FF4D4F");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.Verify(n => n.NotifyPersonaUpdated(It.IsAny<Guid>(), It.IsAny<StoryMapPersonaDto>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
