using Wayd.Common.Domain.Enums.Work;
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
        map.OwnerId.Should().NotBeNullOrWhiteSpace();
        map.Status.Should().Be(WorkStatusCategory.Active);
    }

    [Fact]
    public void With_Overrides_ShouldApply()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid().ToString();

        // Act
        var map = new StoryMapFaker()
            .WithId(id)
            .WithKey(42)
            .WithName("Named map")
            .WithOwnerId(ownerId)
            .WithStatus(WorkStatusCategory.Removed)
            .Generate();

        // Assert
        map.Id.Should().Be(id);
        map.Key.Should().Be(42);
        map.Name.Should().Be("Named map");
        map.OwnerId.Should().Be(ownerId);
        map.Status.Should().Be(WorkStatusCategory.Removed);
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
        map.Status.Should().Be(WorkStatusCategory.Active);
    }
}
