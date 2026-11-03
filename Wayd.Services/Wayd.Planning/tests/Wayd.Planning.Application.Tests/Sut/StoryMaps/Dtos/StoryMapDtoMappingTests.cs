using Wayd.Planning.Domain.Tests.Data;
using Mapster;
using NodaTime;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Dtos;

/// <summary>
/// Verifies the Story Maps Mapster mappings — the IMapFrom-driven projections and the custom
/// ConfigureMapping overrides (task checklist counts, status display-name) — produce the expected
/// DTO shapes when the application's Mapster config is registered.
/// </summary>
public class StoryMapDtoMappingTests
{
    // Scoped config — never mutate GlobalSettings from tests (it leaks into every other test's
    // ProjectToType behavior).
    private static readonly TypeAdapterConfig Config = MapsterTestConfiguration.Config;

    private static StoryMap CreateMap() =>
        StoryMapFakerExtensions.CreateSeeded("My Map", "Desc", null, "Goal", "Step").WithPeopleLinked();

    [Fact]
    public void StoryMapDetailsDto_MapsStatusToDisplayName()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var dto = map.Adapt<StoryMapDetailsDto>(Config);

        // Assert
        dto.Status.Should().Be(StatusCategory.Active.GetDisplayName());
    }

    [Fact]
    public void StoryMapDetailsDto_MapsFullGraph()
    {
        // Arrange
        var map = CreateMap();
        var goalId = map.Goals.Single().Id;
        var stepId = map.Goals.Single().Steps.Single().Id;
        map.AddTask(stepId, "A task", null, StoryMapActors.Owner);

        // Act
        var dto = map.Adapt<StoryMapDetailsDto>(Config);

        // Assert
        dto.Id.Should().Be(map.Id);
        dto.Goals.Should().ContainSingle();
        dto.Goals.Single().Id.Should().Be(goalId);
        dto.Goals.Single().Steps.Should().ContainSingle();
        dto.Goals.Single().Steps.Single().Tasks.Should().ContainSingle(t => t.Title == "A task");
        dto.SwimLanes.Should().ContainSingle(l => l.IsDefault);
    }

    [Fact]
    public void StoryMapTaskDto_MapsChecklistCompletionCounts()
    {
        // Arrange — a task with 3 checklist items, 2 of them checked, maps to 2/3.
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Scoped task", null, StoryMapActors.Owner).Value;
        var item1 = map.AddChecklistItem(task.Id, "one", StoryMapActors.Owner).Value;
        var item2 = map.AddChecklistItem(task.Id, "two", StoryMapActors.Owner).Value;
        map.AddChecklistItem(task.Id, "three", StoryMapActors.Owner);
        map.SetChecklistItemChecked(task.Id, item1.Id, true, StoryMapActors.Owner);
        map.SetChecklistItemChecked(task.Id, item2.Id, true, StoryMapActors.Owner);

        // Act
        var dto = task.Adapt<StoryMapTaskDto>(Config);

        // Assert
        dto.ChecklistTotalCount.Should().Be(3);
        dto.ChecklistCompletedCount.Should().Be(2);
        dto.Checklist.Should().HaveCount(3);
    }

    [Fact]
    public void StoryMapTaskDto_MapsPersonaTagsAndWorkItemLink()
    {
        // Arrange
        var map = CreateMap();
        var stepId = map.Goals.Single().Steps.Single().Id;
        var task = map.AddTask(stepId, "Linked task", null, StoryMapActors.Owner).Value;
        var persona = map.AddPersona("Field tech", null, "#4096FF", StoryMapActors.Owner).Value;
        map.SetTaskPersonas(task.Id, [persona.Id], StoryMapActors.Owner);
        map.LinkWorkItem(task.Id, 4242, StoryMapActors.Owner);

        // Act
        var dto = task.Adapt<StoryMapTaskDto>(Config);

        // Assert
        dto.PersonaIds.Should().ContainSingle().Which.Should().Be(persona.Id);
        dto.LinkedWorkItemId.Should().Be(4242);
    }

    [Fact]
    public void StoryMapSwimLaneDto_MapsDescriptiveDates()
    {
        // Arrange
        var map = CreateMap();
        var lane = map.AddSwimLane("Release 1", StoryMapActors.Owner).Value;
        var start = new LocalDate(2026, 1, 1);
        var end = new LocalDate(2026, 3, 31);
        map.SetSwimLaneDates(lane.Id, start, end, StoryMapActors.Owner);

        // Act
        var dto = lane.Adapt<StoryMapSwimLaneDto>(Config);

        // Assert
        dto.Name.Should().Be("Release 1");
        dto.IsDefault.Should().BeFalse();
        dto.StartDate.Should().Be(start);
        dto.EndDate.Should().Be(end);
    }

    [Fact]
    public void StoryMapPersonaDto_MapsNameDescriptionColor()
    {
        // Arrange
        var map = CreateMap();
        var persona = map.AddPersona("Dispatcher", "Routes jobs", "#52C41A", StoryMapActors.Owner).Value;

        // Act
        var dto = persona.Adapt<StoryMapPersonaDto>(Config);

        // Assert
        dto.Name.Should().Be("Dispatcher");
        dto.Description.Should().Be("Routes jobs");
        dto.Color.Should().Be("#52C41A");
    }

    [Fact]
    public void StoryMapListDto_MapsStatus()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var dto = map.Adapt<StoryMapListDto>(Config);

        // Assert
        dto.Status.Should().Be(StatusCategory.Active.GetDisplayName());
    }

    [Fact]
    public void StoryMapDetailsDto_MapsAccess()
    {
        // Arrange
        var editorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var map = new StoryMapFaker()
            .WithVisibility(Visibility.Private)
            .WithEditAccess(StoryMapEditAccess.OwnersAndEditors)
            .WithEditors(editorId)
            .WithEditorTeams(teamId)
            .Generate()
            .WithPeopleLinked();

        // Act
        var dto = map.Adapt<StoryMapDetailsDto>(Config);

        // Assert — CanManage and CanEdit are the caller's, which the query sets.
        dto.Visibility.Should().Be(Visibility.Private);
        dto.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
        dto.Owners.Select(o => o.Id).Should().Equal(StoryMapActors.OwnerEmployeeId);
        dto.Editors.Select(e => e.Id).Should().Equal(editorId);
        dto.EditorTeams.Select(t => t.Id).Should().Equal(teamId);
        dto.CanManage.Should().BeFalse();
        dto.CanEdit.Should().BeFalse();
    }

    [Fact]
    public void StoryMapListDto_MapsOwnersAndAccess()
    {
        // Arrange
        var map = new StoryMapFaker().WithVisibility(Visibility.Private).Generate().WithPeopleLinked();

        // Act
        var dto = map.Adapt<StoryMapListDto>(Config);

        // Assert
        dto.Visibility.Should().Be(Visibility.Private);
        dto.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);
        dto.Owners.Select(o => o.Id).Should().Equal(StoryMapActors.OwnerEmployeeId);
    }
}
