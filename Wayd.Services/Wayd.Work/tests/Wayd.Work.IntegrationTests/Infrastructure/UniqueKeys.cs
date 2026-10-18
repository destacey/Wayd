namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Hands out the keys seeded records must hold uniquely, such as a team's key and code, one per call for the whole
/// test run.
/// </summary>
/// <remarks>
/// The test classes share one database, so a random key collides with one another class seeded once enough
/// records are added, and the save fails on the unique constraint.
/// </remarks>
public static class UniqueKeys
{
    private static int _last = 100_000;

    /// <summary>A key no earlier call in this run has returned.</summary>
    public static int Next() => Interlocked.Increment(ref _last);
}
