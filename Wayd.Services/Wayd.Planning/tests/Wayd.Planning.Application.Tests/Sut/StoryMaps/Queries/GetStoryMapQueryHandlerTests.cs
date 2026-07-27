using Wayd.Common.Application.Models;
using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Queries;

/// <summary>
/// Covers the detail query: a map resolves by id or by key, a missing map is null rather than an
/// error, and the full graph is mapped through.
/// </summary>
public class GetStoryMapQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly GetStoryMapQueryHandler _handler;

    public GetStoryMapQueryHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _handler = new GetStoryMapQueryHandler(_dbContext);
    }

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("Map", "Desc", Guid.NewGuid().ToString(), "Goal", "Step");

    [Fact]
    public async Task Handle_ShouldReturnTheMap_WhenFoundById()
    {
        // Arrange
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey(map.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(map.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheMap_WhenFoundByKey()
    {
        // Arrange
        var map = new StoryMapFaker().WithKey(42).Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey("42")),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(42);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNotFound()
    {
        // Arrange — no error: the controller turns a null into a 404.
        var map = CreateMap();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey(Guid.NewGuid().ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnAnArchivedMap()
    {
        // Arrange — unlike the list query, the detail query does not filter by status. An archived
        // map is still readable; it is only writes that are blocked.
        var map = CreateMap();
        map.Archive();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey(map.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldMapTheGoalStepAndTaskGraph()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        map.AddTask(stepId, "A task");
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey(map.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var step = result!.Goals.Single().Steps.Single();
        step.Tasks.Should().ContainSingle(t => t.Title == "A task");
    }

    [Fact]
    public async Task Handle_ShouldMapLanesAndPersonas()
    {
        // Arrange
        var map = CreateMap();
        map.AddSwimLane("Release 1");
        map.AddPersona("Engineer", null, "#4096FF");
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapQuery(new IdOrKey(map.Id.ToString())),
            TestContext.Current.CancellationToken);

        // Assert — the default lane plus the one just added.
        result.Should().NotBeNull();
        result!.SwimLanes.Should().HaveCount(2);
        result.Personas.Should().ContainSingle(p => p.Name == "Engineer");
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
