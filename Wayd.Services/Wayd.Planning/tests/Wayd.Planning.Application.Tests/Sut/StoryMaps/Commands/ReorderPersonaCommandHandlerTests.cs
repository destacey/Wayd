using Wayd.Planning.Domain.Tests.Data;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class ReorderPersonaCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly ReorderPersonaCommandHandler _handler;
    private readonly Mock<ILogger<ReorderPersonaCommandHandler>> _mockLogger;
    private readonly Mock<IStoryMapNotifier> _mockNotifier;

    public ReorderPersonaCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ReorderPersonaCommandHandler>>();
        _mockNotifier = new Mock<IStoryMapNotifier>();

        _handler = new ReorderPersonaCommandHandler(_dbContext, _mockNotifier.Object, _mockLogger.Object);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldReorderPersona_WhenMapExists()
    {
        // Arrange
        var map = CreateMap();
        var first = map.AddPersona("First", null, "#4096FF").Value;
        var second = map.AddPersona("Second", null, "#52C41A").Value;
        var third = map.AddPersona("Third", null, "#FA8C16").Value;
        _dbContext.AddStoryMap(map);

        var command = new ReorderPersonaCommand(map.Id, third.Id, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        map.Personas.Select(p => p.Id).Should().Equal(third.Id, first.Id, second.Id);
        map.Personas.Select(p => p.Order).Should().Equal(0, 1, 2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotifyWithTheAppliedOrder_WhenTheRequestedOrderIsClamped()
    {
        // Arrange — an order past the end is clamped by the domain, so the broadcast must carry
        // where the persona landed rather than what was asked for.
        var map = CreateMap();
        var first = map.AddPersona("First", null, "#4096FF").Value;
        map.AddPersona("Second", null, "#52C41A");
        _dbContext.AddStoryMap(map);

        var command = new ReorderPersonaCommand(map.Id, first.Id, 99);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockNotifier.Verify(n => n.NotifyPersonaReordered(map.Id, first.Id, 1), Times.Once);
        _mockNotifier.Verify(n => n.NotifyPersonaReordered(map.Id, first.Id, 99), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenMapNotFound()
    {
        // Arrange
        var command = new ReorderPersonaCommand(Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Story map not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPersonaNotFound()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        var command = new ReorderPersonaCommand(map.Id, Guid.NewGuid(), 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheMapIsArchived()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("First", null, "#4096FF").Value;
        map.Archive();
        _dbContext.AddStoryMap(map);

        var command = new ReorderPersonaCommand(map.Id, persona.Id, 0);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This story map is archived and cannot be changed.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
