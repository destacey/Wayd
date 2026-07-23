namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Knobs for the generated PPM dataset. It is layered on top of a generated organization, so most of its
/// size comes from the org (a portfolio per value stream, projects per team); these control the extras.
/// </summary>
public sealed class PpmOptions
{
    /// <summary>
    /// Number of cross-cutting business-function portfolios generated in addition to the one-per-value-stream
    /// portfolios. These pull projects from teams across value streams. Zero produces value-stream portfolios only.
    /// </summary>
    public int FunctionPortfolios { get; init; } = 2;

    /// <summary>
    /// Average number of projects an ART has in flight at any one time. Projects are scoped at the ART — most
    /// are a subset of the ART's teams collaborating, and a minority reach across ARTs — so the ART, not the
    /// team, sets the load. The total number of projects generated per ART is derived from this: enough that
    /// roughly this many overlap at any point across the four-year window, which yields a rich history and a
    /// full runway rather than a handful of projects total.
    /// </summary>
    public int ConcurrentProjectsPerArt { get; init; } = 10;

    /// <summary>
    /// Average number of thematic programs a portfolio has running at once. Programs are the portfolio's
    /// investment-level groupings of projects (Modernization, Integrations, …) — independent of the delivery
    /// hierarchy — and each runs 1-3 years, so like projects the total generated is derived from this across
    /// the window. Capped at the number of available program themes so concurrent programs stay distinct.
    /// </summary>
    public int ConcurrentProgramsPerPortfolio { get; init; } = 5;

    /// <summary>The random seed. Shared with the org generator so a single seed reproduces the whole dataset.</summary>
    public int Seed { get; init; }
}
