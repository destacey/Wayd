namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>
/// The shape every area shares: say what it depends on, say whether it has work, do the work.
/// </summary>
/// <remarks>
/// Areas name themselves with an <c>area.thing</c> key. The prefix is the area of Wayd the records belong
/// to, so a new one — product management, planning — adds a sibling set rather than editing an ordering
/// anyone has to keep in their head.
/// </remarks>
public abstract class SeedArea(string name, params string[] dependsOn) : ISeedArea
{
    public string Name { get; } = name;

    public IReadOnlyList<string> DependsOn { get; } = dependsOn;

    public abstract bool ShouldRun(SeedContext context);

    public abstract Task Run(SeedContext context, CancellationToken cancellationToken);
}
