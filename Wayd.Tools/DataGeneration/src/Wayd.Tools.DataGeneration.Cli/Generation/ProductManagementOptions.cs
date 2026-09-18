namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Knobs for the generated Product Management dataset. Like PPM it is layered over the organization — a
/// product line per value stream, a product per ART, a component per team — so the catalog's size comes
/// from the org, and these shape how the catalog ships.
/// </summary>
public sealed class ProductManagementOptions
{
    /// <summary>
    /// Average days between two versions of a service. Other kinds of component scale from it — web
    /// applications ship less often, libraries and tools far less — and every team runs at its own tempo
    /// around it, speeding up across the history.
    /// </summary>
    public int VersionIntervalDays { get; init; } = 14;

    /// <summary>
    /// The share of production deployments that fail or are rolled back, averaged over the history. Teams
    /// vary around it, and the rate falls as the history approaches today.
    /// </summary>
    public double ChangeFailureRate { get; init; } = 0.12;

    /// <summary>
    /// The share of ARTs that ship their services together as release packages on a train, rather than
    /// each service deploying on its own. Any share above zero packages at least one ART.
    /// </summary>
    public double PackagedArtFraction { get; init; } = 0.25;

    /// <summary>
    /// Average components each team owns. At the default the catalog is what it has always been — about one
    /// deployable per team, some with a library or tool beside it — and draws nothing extra, so a pinned seed
    /// reproduces. Above it, teams own more services: a real microservice estate runs three to ten per team.
    /// </summary>
    public double ComponentsPerTeam { get; init; } = ProductCatalog.BaselineComponentsPerTeam;

    /// <summary>Whether products record what they depend on.</summary>
    public bool Dependencies { get; init; } = true;

    /// <summary>
    /// Average products each component depends on. Kinds vary around it: applications call more, libraries
    /// almost nothing.
    /// </summary>
    public double DependenciesPerComponent { get; init; } = 3.5;

    /// <summary>The share of dependencies a product cannot work without, before each link's own leaning.</summary>
    public double HardDependencyFraction { get; init; } = 0.6;

    /// <summary>
    /// How many shared platform services most of the catalog relies on. Null sizes it from the catalog, at
    /// about one service in forty.
    /// </summary>
    public int? PlatformServices { get; init; }
}
