using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Planning.Domain.Enums;
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
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext;
    private readonly GetStoryMapsQueryHandler _handler;

    public GetStoryMapsQueryHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _handler = new GetStoryMapsQueryHandler(_dbContext, _access);
    }

    private static StoryMap CreateMap(string name, int key) =>
        new StoryMapFaker().WithName(name).WithKey(key).Generate().WithPeopleLinked();

    private static StoryMap CreateArchivedMap(string name, int key)
    {
        var map = CreateMap(name, key);
        map.Archive(StoryMapActors.Owner);
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

    [Theory]
    [InlineData("editor", 2)]
    [InlineData("editor team member", 2)]
    [InlineData("viewer", 1)]
    [InlineData("no employee", 1)]
    [InlineData("administrator", 3)]
    public async Task Handle_ShouldListPublicMapsAndThePrivateOnesTheReaderIsOn(string reader, int expected)
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var publicMap = CreateMap("Public", 1);
        var privateWithEditors = new StoryMapFaker().WithKey(2).WithVisibility(Visibility.Private)
            .WithOwners(Guid.NewGuid()).WithEditors(editorId).WithEditorTeams(teamId).Generate().WithPeopleLinked();
        var privateOfSomeoneElse = new StoryMapFaker().WithKey(3).WithVisibility(Visibility.Private)
            .WithOwners(Guid.NewGuid()).Generate().WithPeopleLinked();
        _dbContext.AddStoryMaps([publicMap, privateWithEditors, privateOfSomeoneElse]);
        _access.Reader = reader switch
        {
            "editor" => new StoryMapReader(editorId, false, [], false),
            "editor team member" => new StoryMapReader(Guid.NewGuid(), false, [teamId], false),
            "viewer" => new StoryMapReader(Guid.NewGuid(), false, [], false),
            "no employee" => new StoryMapReader(null, false, [], false),
            _ => new StoryMapReader(Guid.NewGuid(), true, [], false),
        };

        // Act
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(expected);
        result.Should().Contain(m => m.Id == publicMap.Id);
        if (expected == 1)
            result.Should().NotContain(m => m.Id == privateWithEditors.Id);
    }

    [Fact]
    public async Task Handle_ShouldSayWhatTheReaderCanDoOnEachMap()
    {
        // Arrange
        var owned = CreateMap("Owned", 1);
        var openToViewers = new StoryMapFaker().WithKey(2).WithOwners(Guid.NewGuid()).Generate().WithPeopleLinked();
        var ownersAndEditorsOnly = new StoryMapFaker().WithKey(3).WithOwners(Guid.NewGuid())
            .WithEditAccess(StoryMapEditAccess.OwnersAndEditors).Generate().WithPeopleLinked();
        _dbContext.AddStoryMaps([owned, openToViewers, ownersAndEditorsOnly]);

        // Act — the default reader owns generated maps.
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single(m => m.Id == owned.Id).Should().Match<StoryMapListDto>(m => m.CanManage && m.CanEdit);
        result.Single(m => m.Id == openToViewers.Id).Should().Match<StoryMapListDto>(m => !m.CanManage && m.CanEdit);
        result.Single(m => m.Id == ownersAndEditorsOnly.Id).Should().Match<StoryMapListDto>(m => !m.CanManage && !m.CanEdit);
    }

    [Fact]
    public async Task Handle_ReadOnlyToken_ShouldNeitherManageNorEdit()
    {
        // Arrange
        _dbContext.AddStoryMap(CreateMap("Owned", 1));
        _access.Reader = new StoryMapReader(StoryMapActors.OwnerEmployeeId, false, [], true);

        // Act
        var result = await _handler.Handle(new GetStoryMapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single().CanManage.Should().BeFalse();
        result.Single().CanEdit.Should().BeFalse();
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
