using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions;

/// <summary>
/// Runs a change to one person's part in a poker session — joining, voting, turning voting on or off — so that two of
/// their requests racing each other both end consistently. The one that loses is re-applied against fresh state, where
/// it sees the winner's change: a join finds the member the other request inserted, and a vote or a change to voting
/// sees whether the person is still voting and which votes they hold.
/// </summary>
internal static class PokerSessionRacingSave
{
    /// <summary>
    /// Runs <paramref name="attempt"/>, running it once more if its save met another request's row at a unique index or
    /// found the member row changed since it was read (see <see cref="TouchMember"/>). <paramref name="attempt"/> must
    /// load the session afresh each time it is called; a second collision propagates.
    /// </summary>
    public static async Task<T> RetryOnce<T>(IPlanningDbContext dbContext, Func<Task<T>> attempt)
    {
        try
        {
            return await attempt();
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || DbUpdateErrors.IsUniqueViolation(ex))
        {
            // Discard the rejected write, or the reload returns the tracked graph and the save repeats it.
            dbContext.ChangeTracker?.Clear();
            return await attempt();
        }
    }

    /// <summary>
    /// Puts the member's row in a vote's save, so its row version is checked and bumped. A vote is a row of its own
    /// and leaves the member untouched, so without this a vote and turning voting off, each read before the other
    /// saved, both commit: a member who is not voting, holding a vote in an unrevealed round.
    /// </summary>
    public static void TouchMember(IPlanningDbContext dbContext, PokerSessionMember member)
    {
        try
        {
            var entry = dbContext.Entry(member);
            if (entry.State == EntityState.Unchanged)
                entry.State = EntityState.Modified;
        }
        catch (NotImplementedException)
        {
            // The unit-test fakes have no change tracker. Only conflict detection depends on this, so the save still
            // stands without it.
        }
    }
}
