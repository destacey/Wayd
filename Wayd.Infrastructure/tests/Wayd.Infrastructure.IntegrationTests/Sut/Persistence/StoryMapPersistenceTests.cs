using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Domain.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Saves a story map's owners and editors, and reads maps back through the access filters, the DTO mappings and a
/// content command. The fake contexts run all of these in memory, where a missing <c>.Include</c> goes unnoticed, so
/// only SQL Server shows that the handlers load what the map decides access with.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StoryMapPersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Lazy<bool> PlanningMappings = new(() =>
    {
        var assembly = typeof(StoryMapDetailsDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private async Task<StoryMap> SaveMap(Guid ownerId, Visibility visibility, StoryMapEditAccess editAccess, Action<StoryMap, StoryMapActor> arrange, CancellationToken ct)
    {
        var map = StoryMap.Create($"Map {Guid.NewGuid():N}"[..20], null, ownerId, visibility, editAccess).Value;
        var owner = new StoryMapActor(ownerId, false, new HashSet<Guid>());
        map.AddGoal("Goal", owner).IsSuccess.Should().BeTrue();
        arrange(map, owner);

        await using var context = _fixture.CreateContext();
        context.StoryMaps.Add(map);
        await context.SaveChangesAsync(ct);
        return map;
    }

    private static Mock<IStoryMapAccess> AccessOf(StoryMapReader reader, StoryMapActor? actor = null)
    {
        var access = new Mock<IStoryMapAccess>();
        access.Setup(a => a.ResolveReader(It.IsAny<CancellationToken>())).ReturnsAsync(reader);
        if (actor is not null)
            access.Setup(a => a.ResolveActor(It.IsAny<StoryMap>(), It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        return access;
    }

    [Fact]
    public async Task GetStoryMap_MapsOwnersEditorsAndTeams_AndWhatTheReaderCanDo()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var editor = await PlanningTestData.SeedEmployee(_fixture, ct);
        var team = await PlanningTestData.SeedTeam(_fixture, ct);
        var map = await SaveMap(owner.Id, Visibility.Private, StoryMapEditAccess.OwnersAndEditors, (m, actor) =>
            m.SetEditors([editor.Id], [team.Id], actor).IsSuccess.Should().BeTrue(), ct);

        async Task<StoryMapDetailsDto?> Read(StoryMapReader reader)
        {
            await using var read = _fixture.CreateContext();
            return await new GetStoryMapQueryHandler(read, AccessOf(reader).Object).Handle(new GetStoryMapQuery(map.Key.ToString()), ct);
        }

        // Act
        var forOwner = await Read(new(owner.Id, false, [], false));
        var forEditor = await Read(new(editor.Id, false, [], false));
        var forOutsider = await Read(new(Guid.NewGuid(), false, [], false));

        // Assert
        forOwner.Should().NotBeNull();
        forOwner!.Visibility.Should().Be(Visibility.Private);
        forOwner.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
        forOwner.Owners.Select(o => o.Id).Should().Equal(owner.Id);
        forOwner.Owners.Single().Name.Should().NotBeNullOrWhiteSpace();
        forOwner.Editors.Select(e => e.Id).Should().Equal(editor.Id);
        forOwner.EditorTeams.Select(t => (t.Id, t.Name)).Should().Equal((team.Id, team.Name));
        (forOwner.CanManage, forOwner.CanEdit).Should().Be((true, true));
        (forEditor!.CanManage, forEditor.CanEdit).Should().Be((false, true));
        forOutsider.Should().BeNull();
    }

    [Fact]
    public async Task GetStoryMaps_FiltersPrivateMapsAndSaysWhoCanEditThem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var editor = await PlanningTestData.SeedEmployee(_fixture, ct);
        var teammate = await PlanningTestData.SeedEmployee(_fixture, ct);
        var outsider = await PlanningTestData.SeedEmployee(_fixture, ct);
        var team = await PlanningTestData.SeedTeam(_fixture, ct);
        var openMap = await SaveMap(owner.Id, Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView, (_, _) => { }, ct);
        var restrictedMap = await SaveMap(owner.Id, Visibility.Public, StoryMapEditAccess.OwnersAndEditors, (_, _) => { }, ct);
        var privateMap = await SaveMap(owner.Id, Visibility.Private, StoryMapEditAccess.AnyoneWhoCanView, (m, actor) =>
            m.SetEditors([editor.Id], [team.Id], actor).IsSuccess.Should().BeTrue(), ct);
        Guid[] ours = [openMap.Id, restrictedMap.Id, privateMap.Id];

        async Task<Dictionary<Guid, (bool CanManage, bool CanEdit)>> Listed(StoryMapReader reader)
        {
            await using var read = _fixture.CreateContext();
            var maps = await new GetStoryMapsQueryHandler(read, AccessOf(reader).Object).Handle(new GetStoryMapsQuery(), ct);
            return maps.Where(m => ours.Contains(m.Id)).ToDictionary(m => m.Id, m => (m.CanManage, m.CanEdit));
        }

        // Act
        var forOwner = await Listed(new(owner.Id, false, [], false));
        var forEditor = await Listed(new(editor.Id, false, [], false));
        var forTeammate = await Listed(new(teammate.Id, false, [team.Id], false));
        var forOutsider = await Listed(new(outsider.Id, false, [], false));
        var forNoEmployee = await Listed(new(null, false, [], false));
        var forAdministrator = await Listed(new(outsider.Id, true, [], false));

        // Assert
        forOwner.Values.Should().AllSatisfy(v => v.Should().Be((true, true)));
        forOwner.Keys.Should().BeEquivalentTo(ours);
        forEditor[privateMap.Id].Should().Be((false, true));
        forTeammate[privateMap.Id].Should().Be((false, true));
        forOutsider.Keys.Should().BeEquivalentTo([openMap.Id, restrictedMap.Id]);
        forOutsider[openMap.Id].Should().Be((false, true));
        forOutsider[restrictedMap.Id].Should().Be((false, false));
        forNoEmployee.Keys.Should().BeEquivalentTo([openMap.Id, restrictedMap.Id]);
        forNoEmployee.Values.Should().AllSatisfy(v => v.Should().Be((false, false)));
        forAdministrator.Values.Should().AllSatisfy(v => v.Should().Be((true, true)));
    }

    [Fact]
    public async Task ContentCommand_ByAnEditorOnAnOwnersAndEditorsMap_LoadsTheEditorsAndSucceeds()
    {
        // Arrange — a handler that loaded the map without its editors would refuse this editor.
        var ct = TestContext.Current.CancellationToken;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var editor = await PlanningTestData.SeedEmployee(_fixture, ct);
        var map = await SaveMap(owner.Id, Visibility.Private, StoryMapEditAccess.OwnersAndEditors, (m, actor) =>
            m.SetEditors([editor.Id], [], actor).IsSuccess.Should().BeTrue(), ct);
        var actor = new StoryMapActor(editor.Id, false, new HashSet<Guid>());

        await using var write = _fixture.CreateContext();
        var handler = new AddGoalCommandHandler(write, AccessOf(new(editor.Id, false, [], false), actor).Object, Mock.Of<IStoryMapNotifier>(), NullLogger<AddGoalCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddGoalCommand(map.Id, "Second goal"), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var read = _fixture.CreateContext();
        (await read.StoryMaps.AsNoTracking().Include(m => m.Goals).SingleAsync(m => m.Id == map.Id, ct))
            .Goals.Select(g => g.Name).Should().Contain("Second goal");
    }

    [Fact]
    public async Task ReorderCommand_ByAnEditorTeamMember_LoadsTheTeamsThroughTheRetryingMutation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var teammate = await PlanningTestData.SeedEmployee(_fixture, ct);
        var team = await PlanningTestData.SeedTeam(_fixture, ct);
        var map = await SaveMap(owner.Id, Visibility.Private, StoryMapEditAccess.OwnersAndEditors, (m, actor) =>
        {
            m.SetEditors([], [team.Id], actor).IsSuccess.Should().BeTrue();
            m.AddGoal("Second goal", actor).IsSuccess.Should().BeTrue();
        }, ct);
        var goalId = map.Goals.Single(g => g.Name == "Second goal").Id;
        var actor = new StoryMapActor(teammate.Id, false, new HashSet<Guid> { team.Id });

        await using var write = _fixture.CreateContext();
        var handler = new ReorderGoalCommandHandler(write, AccessOf(new(teammate.Id, false, [team.Id], false), actor).Object, Mock.Of<IStoryMapNotifier>(), NullLogger<ReorderGoalCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new ReorderGoalCommand(map.Id, goalId, 0), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public async Task LoadingWithoutIncludeAccess_LeavesTheEditorsEmptyAndRefusesTheEditor()
    {
        // Arrange — the trap IncludeAccess exists for: nothing fails, the editor just reads as an outsider.
        var ct = TestContext.Current.CancellationToken;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var editor = await PlanningTestData.SeedEmployee(_fixture, ct);
        var map = await SaveMap(owner.Id, Visibility.Private, StoryMapEditAccess.OwnersAndEditors, (m, actor) =>
            m.SetEditors([editor.Id], [], actor).IsSuccess.Should().BeTrue(), ct);
        var actor = new StoryMapActor(editor.Id, false, new HashSet<Guid>());

        // Act
        await using var read = _fixture.CreateContext();
        var bare = await read.StoryMaps.AsNoTracking().SingleAsync(m => m.Id == map.Id, ct);
        await using var readWithAccess = _fixture.CreateContext();
        var loaded = await readWithAccess.StoryMaps.AsNoTracking().IncludeAccess().SingleAsync(m => m.Id == map.Id, ct);

        // Assert
        bare.CanEdit(actor).Should().BeFalse();
        loaded.CanEdit(actor).Should().BeTrue();
        loaded.IsOwner(owner.Id).Should().BeTrue();
    }

    [Fact]
    public async Task SetOwners_PersistsTheNewOwners()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var owner = await PlanningTestData.SeedEmployee(_fixture, ct);
        var next = await PlanningTestData.SeedEmployee(_fixture, ct);
        var map = await SaveMap(owner.Id, Visibility.Public, StoryMapEditAccess.AnyoneWhoCanView, (_, _) => { }, ct);
        var actor = new StoryMapActor(owner.Id, false, new HashSet<Guid>());

        await using var write = _fixture.CreateContext();
        var handler = new SetStoryMapOwnersCommandHandler(write, AccessOf(new(owner.Id, false, [], false), actor).Object, Mock.Of<IStoryMapNotifier>(), NullLogger<SetStoryMapOwnersCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new SetStoryMapOwnersCommand(map.Id, [next.Id]), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var read = _fixture.CreateContext();
        (await read.StoryMaps.AsNoTracking().IncludeAccess().SingleAsync(m => m.Id == map.Id, ct))
            .Owners.Select(o => o.EmployeeId).Should().Equal(next.Id);
    }
}
