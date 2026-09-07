namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// One unit of a seed: reads what it needs from the context, writes its file, waits for the run, and
/// publishes the ids it created.
/// </summary>
/// <remarks>
/// Areas declare what they depend on rather than sitting in a hand-maintained order. The order a seed
/// actually needs is not a matter of taste — a program cannot be created before its portfolio exists, and
/// the finalize pass cannot run until everything it closes has landed — so stating the dependency and
/// deriving the order keeps the two from drifting as areas are added.
/// </remarks>
public interface ISeedArea
{
    /// <summary>Stable name, used to declare dependencies and to key the ids this area publishes.</summary>
    string Name { get; }

    /// <summary>The areas that must have completed before this one runs.</summary>
    IReadOnlyList<string> DependsOn { get; }

    /// <summary>
    /// Whether this area has anything to do for the current context — a seed with no programs skips the
    /// program area rather than posting an empty file, which the endpoints reject.
    /// </summary>
    bool ShouldRun(SeedContext context);

    Task Run(SeedContext context, CancellationToken cancellationToken);
}
