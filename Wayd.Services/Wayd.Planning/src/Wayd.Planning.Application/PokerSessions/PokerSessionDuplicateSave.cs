namespace Wayd.Planning.Application.PokerSessions;

/// <summary>
/// Runs a poker session change that inserts a row only the first time — a member on joining, a vote on first
/// voting — so that two requests racing to insert it both succeed. The one that loses at the unique index is
/// re-applied against fresh state, where it finds the winner's row and changes that instead.
/// </summary>
internal static class PokerSessionDuplicateSave
{
    /// <summary>
    /// Runs <paramref name="attempt"/>, running it once more if its save met another request's row at a unique
    /// index. <paramref name="attempt"/> must load the session afresh each time it is called; a second collision
    /// propagates.
    /// </summary>
    public static async Task<T> RetryOnce<T>(IPlanningDbContext dbContext, Func<Task<T>> attempt)
    {
        try
        {
            return await attempt();
        }
        catch (DbUpdateException ex) when (DbUpdateErrors.IsUniqueViolation(ex))
        {
            // Discard the rejected insert, or the reload returns the tracked graph and the save repeats it.
            dbContext.ChangeTracker?.Clear();
            return await attempt();
        }
    }
}
