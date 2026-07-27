using Microsoft.EntityFrameworkCore.ChangeTracking;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps;

/// <summary>
/// Runs a story map mutation, reloading and re-applying it once if someone else changed the map in
/// between. The retry re-runs <c>mutate</c> against fresh state, so its rules (does this step still
/// exist, is the map archived) are re-evaluated rather than assumed; a second conflict throws.
/// </summary>
/// <remarks>
/// Used by the reorder and move commands, where collaborators dragging at the same time would
/// otherwise renumber from stale positions and leave a visibly wrong order. Everything else is
/// last-write-wins: those commands target a single field or node, so a race loses one edit rather
/// than corrupting a sequence, and the SignalR refetch converges every viewer.
/// </remarks>
public static class StoryMapMutation
{
    /// <summary>
    /// Loads the map, applies <paramref name="mutate"/>, and saves — retrying once on a concurrency
    /// conflict. <paramref name="load"/> must return a freshly-tracked graph each time it is called.
    /// </summary>
    public static async Task<Result<T>> Apply<T>(
        IPlanningDbContext dbContext,
        Func<CancellationToken, Task<StoryMap?>> load,
        Func<StoryMap, Result<T>> mutate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Run(dbContext, load, mutate, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Discard the stale graph before reloading, or EF returns the tracked copy.
            dbContext.ChangeTracker?.Clear();
            return await Run(dbContext, load, mutate, cancellationToken);
        }
    }

    /// <summary>Result-less overload for commands that only report success or failure.</summary>
    public static async Task<Result> Apply(
        IPlanningDbContext dbContext,
        Func<CancellationToken, Task<StoryMap?>> load,
        Func<StoryMap, Result> mutate,
        CancellationToken cancellationToken)
    {
        var result = await Apply(
            dbContext,
            load,
            map => mutate(map).Map(() => 0),
            cancellationToken);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    private static async Task<Result<T>> Run<T>(
        IPlanningDbContext dbContext,
        Func<CancellationToken, Task<StoryMap?>> load,
        Func<StoryMap, Result<T>> mutate,
        CancellationToken cancellationToken)
    {
        var map = await load(cancellationToken);
        if (map is null)
            return Result.Failure<T>("Story map not found.");

        var result = mutate(map);
        if (result.IsFailure)
            return result;

        MarkAggregateChanged(dbContext, map);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Forces the root row into the write so its rowversion is checked and bumped. Without this, an
    /// edit that only touches a child (moving a task, reordering a step) never reads the root's
    /// version and two such edits interleave undetected.
    /// </summary>
    private static void MarkAggregateChanged(IPlanningDbContext dbContext, StoryMap map)
    {
        try
        {
            var entry = dbContext.Entry(map);
            if (entry.State == EntityState.Unchanged)
                entry.State = EntityState.Modified;
        }
        catch (NotImplementedException)
        {
            // The unit-test fakes have no change tracker. Only conflict detection depends on this,
            // so the save still stands without it.
        }
    }
}
