using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Recognises, and words, the failed save a managed sync gets when two syncs of one source meet at a unique key:
/// both found no record type, work type, workflow, scheme or assignment with the key, and both created one.
/// </summary>
public static class ManagedSyncCollision
{
    /// <summary>Whether the save failed on a unique key, rather than for any other reason a save can fail.</summary>
    public static bool IsCollision(DbUpdateException exception) => DbUpdateErrors.IsUniqueViolation(exception);

    /// <summary>The message for a collision on the managed <paramref name="what"/> keyed <paramref name="key"/>.</summary>
    public static string Message(string what, string key) =>
        $"Unable to save the managed {what} '{key}', most likely because another sync of the same source saved it at the same time. The next sync picks it up.";
}
