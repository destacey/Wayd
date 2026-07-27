using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Queries;

/// <summary>
/// Covers the list query's two behaviours: archived maps are hidden unless asked for, and rows come
/// back newest first.
/// </summary>
public class GetStoryMapsQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly GetStoryMapsQueryHandler _handler;

    public GetStoryMapsQueryHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _handler = new GetStoryMapsQueryHandler(_dbContext);
    }

    private static StoryMap CreateMap(string name, int key) =>
        new StoryMapFaker().WithName(name).WithKey(key).Generate();

    private static StoryMap CreateArchivedMap(string name, int key)
    {
        var map = CreateMap(name, key);
        map.Archive();
        return map;
    }

    [Fact]
    public async Task Handle_ShouldExcludeArchivedMaps_ByDefault()
    {
        // Arrange
        var active = CreateMap("Active", 1);
        var archived = CreateArchivedMap("Archived", 2);
        _dbContext.AddStoryMaps([active, archived]);

        // Act
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(m => m.Id == active.Id);
        result.Should().NotContain(m => m.Id == archived.Id);
    }

    [Fact]
    public async Task Handle_ShouldIncludeArchivedMaps_WhenAsked()
    {
        // Arrange
        var active = CreateMap("Active", 1);
        var archived = CreateArchivedMap("Archived", 2);
        _dbContext.AddStoryMaps([active, archived]);

        // Act
        var result = await _handler.Handle(
            new GetStoryMapsQuery(IncludeArchived: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Id == archived.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnNewestFirst()
    {
        // Arrange — key ascends with creation, so descending key is newest first.
        var oldest = CreateMap("Oldest", 1);
        var newest = CreateMap("Newest", 3);
        var middle = CreateMap("Middle", 2);
        _dbContext.AddStoryMaps([oldest, newest, middle]);

        // Act
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(m => m.Key).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenThereAreNoMaps()
    {
        // Arrange / Act
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
