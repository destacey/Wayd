using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Domain.Tests.Sut.Data;

/// <summary>
/// Confirms the <see cref="StoryMapFaker"/> produces usable instances — the reflection-bound
/// private setters populate, the fluent overrides apply, and the seeded-factory helper yields a
/// real graph.
/// </summary>
public class StoryMapFakerTests
{
    [Fact]
    public void Generate_ShouldPopulateIdentityAndStatus()
    {
        // Act
        var map = new StoryMapFaker().Generate();

        // Assert
        map.Id.Should().NotBe(Guid.Empty);
        map.Key.Should().BeGreaterThan(0);
        map.Name.Should().NotBeNullOrWhiteSpace();
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(StoryMapActors.OwnerEmployeeId);
        map.Visibility.Should().Be(Visibility.Public);
        map.EditAccess.Should().Be(StoryMapEditAccess.AnyoneWhoCanView);
        map.Status.Should().Be(StatusCategory.Active);
    }

    [Fact]
    public void With_Overrides_ShouldApply()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        // Act
        var map = new StoryMapFaker()
            .WithId(id)
            .WithKey(42)
            .WithName("Named map")
            .WithOwners(ownerId)
            .WithEditors(editorId)
            .WithEditorTeams(teamId)
            .WithVisibility(Visibility.Private)
            .WithEditAccess(StoryMapEditAccess.OwnersAndEditors)
            .WithStatus(StatusCategory.Removed)
            .Generate();

        // Assert
        map.Id.Should().Be(id);
        map.Key.Should().Be(42);
        map.Name.Should().Be("Named map");
        map.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(ownerId);
        map.Editors.Should().ContainSingle().Which.EmployeeId.Should().Be(editorId);
        map.EditorTeams.Should().ContainSingle().Which.TeamId.Should().Be(teamId);
        map.Visibility.Should().Be(Visibility.Private);
        map.EditAccess.Should().Be(StoryMapEditAccess.OwnersAndEditors);
        map.Status.Should().Be(StatusCategory.Removed);
    }

    [Fact]
    public void CreateSeeded_ShouldBuildRealGraphWithOneGoalOneStepAndDefaultSwimLane()
    {
        // Act
        var map = StoryMapFakerExtensions.CreateSeeded();

        // Assert
        map.Goals.Should().ContainSingle();
        map.Goals.Single().Steps.Should().ContainSingle();
        map.SwimLanes.Should().ContainSingle(l => l.IsDefault);
        map.Status.Should().Be(StatusCategory.Active);
    }
}
