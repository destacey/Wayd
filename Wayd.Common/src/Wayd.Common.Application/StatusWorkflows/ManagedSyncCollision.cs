namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Recognises, and words, the failed save a managed sync gets when two syncs of one source meet at a unique key:
/// both found no record type, work type, workflow, scheme or assignment with the key, and both created one.
/// </summary>
public static class ManagedSyncCollision
{
    // SQL Server: 2601 = unique index, 2627 = unique constraint.
    private static readonly HashSet<int> UniqueKeyErrors = [2601, 2627];

    /// <summary>Whether the save failed on a unique key, rather than for any other reason a save can fail.</summary>
    /// <remarks>
    /// Reads the provider exception's error number without naming its type: the application layers do not
    /// reference the SQL Server client, whose <c>SqlException</c> carries the number as <c>Number</c>. Infrastructure's
    /// <c>DbUpdateExceptionExtensions.IsUniqueViolation</c> reads the same numbers from the typed exception.
    /// </remarks>
    public static bool IsCollision(DbUpdateException exception) =>
        exception.InnerException?.GetType().GetProperty("Number")?.GetValue(exception.InnerException) is int number
        && UniqueKeyErrors.Contains(number);

    /// <summary>The message for a collision on the managed <paramref name="what"/> keyed <paramref name="key"/>.</summary>
    public static string Message(string what, string key) =>
        $"Unable to save the managed {what} '{key}', most likely because another sync of the same source saved it at the same time. The next sync picks it up.";
}
