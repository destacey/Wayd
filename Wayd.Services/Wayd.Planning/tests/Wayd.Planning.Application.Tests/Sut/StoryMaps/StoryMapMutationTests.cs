using Microsoft.EntityFrameworkCore;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps;

/// <summary>
/// Covers the retry-on-conflict contract of <see cref="StoryMapMutation"/>. A story map is edited by
/// several people at once, so a concurrency conflict on the contended commands (reorders and moves)
/// is an ordinary race — it must be re-applied against fresh state rather than surfaced as an error.
/// </summary>
public class StoryMapMutationTests
{
    private static StoryMap CreateMap()
    {
        var map = StoryMap.Create("My Map", null, Guid.NewGuid().ToString()).Value;
        map.AddGoal("Goal");
        return map;
    }

    private static (FakePlanningDbContext Context, StoryMap Map) BuildContext()
    {
        var map = CreateMap();
        var context = new FakePlanningDbContext();
        context.AddStoryMap(map);
        return (context, map);
    }

    [Fact]
    public async Task Apply_WithNoConflict_ShouldSaveOnce()
    {
        // Arrange
        var (context, map) = BuildContext();

        // Act
        var result = await StoryMapMutation.Apply(
            context,
            _ => Task.FromResult<StoryMap?>(map),
            m => m.AddGoal("Another"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Apply_WhenTheFirstSaveConflicts_ShouldReloadAndSucceed()
    {
        // Arrange — someone else wrote to the map between our read and our write.
        var (context, map) = BuildContext();
        context.ConcurrencyConflictsToThrow = 1;

        var loadCount = 0;

        // Act
        var result = await StoryMapMutation.Apply(
            context,
            _ =>
            {
                loadCount++;
                return Task.FromResult<StoryMap?>(map);
            },
            m => m.RenameGoal(m.Goals[0].Id, "Renamed"),
            TestContext.Current.CancellationToken);

        // Assert — reloaded and re-applied rather than failing the caller.
        result.IsSuccess.Should().BeTrue();
        loadCount.Should().Be(2);
        context.SaveChangesCallCount.Should().Be(2);
        map.Goals[0].Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Apply_WhenTheRetryAlsoConflicts_ShouldThrow()
    {
        // Arrange — two conflicts in a row is genuine contention, not an ordinary race. It reaches
        // the handler's catch, which logs it and returns a failure.
        var (context, map) = BuildContext();
        context.ConcurrencyConflictsToThrow = 2;

        // Act
        var apply = async () => await StoryMapMutation.Apply(
            context,
            _ => Task.FromResult<StoryMap?>(map),
            m => m.AddGoal("Another"),
            TestContext.Current.CancellationToken);

        // Assert
        await apply.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Apply_WhenTheMapIsMissing_ShouldFailWithoutSaving()
    {
        // Arrange
        var context = new FakePlanningDbContext();

        // Act
        var result = await StoryMapMutation.Apply(
            context,
            _ => Task.FromResult<StoryMap?>(null),
            m => m.AddGoal("Goal"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Story map not found.");
        context.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Apply_WhenTheMutationFails_ShouldNotSave()
    {
        // Arrange
        var (context, map) = BuildContext();

        // Act — an id that does not resolve on this map.
        var result = await StoryMapMutation.Apply(
            context,
            _ => Task.FromResult<StoryMap?>(map),
            m => m.RenameGoal(Guid.NewGuid(), "Renamed"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        context.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Apply_OnRetry_ShouldReEvaluateTheMutationAgainstFreshState()
    {
        // Arrange — the map is archived between the first attempt and the retry, so the re-applied
        // mutation must be rejected rather than assuming the original decision still holds.
        var (context, map) = BuildContext();
        context.ConcurrencyConflictsToThrow = 1;

        var attempt = 0;

        // Act
        var result = await StoryMapMutation.Apply(
            context,
            _ =>
            {
                attempt++;
                if (attempt == 2) map.Archive();
                return Task.FromResult<StoryMap?>(map);
            },
            m => m.AddGoal("Another"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This story map is archived and cannot be changed.");
    }
}
