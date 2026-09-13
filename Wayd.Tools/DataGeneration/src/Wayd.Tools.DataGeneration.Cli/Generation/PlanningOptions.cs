namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Knobs for the generated Planning dataset. Layered over the organization like the other areas — a series
/// of planning intervals per ART, rostered with its teams — so how many intervals and teams there are comes
/// from the org and the timeline, and these shape the iterations and what each team plans.
/// </summary>
public sealed class PlanningOptions
{
    /// <summary>
    /// The length of each iteration inside a planning interval, in weeks. Intervals are quarterly, so this
    /// also decides how many iterations each holds.
    /// </summary>
    public int IterationWeeks { get; init; } = 2;

    /// <summary>Average number of objectives a team commits to per planning interval, stretch objectives included.</summary>
    public int ObjectivesPerTeam { get; init; } = 4;

    /// <summary>Average number of risks a team raises per planning interval.</summary>
    public double RisksPerTeam { get; init; } = 1.5;
}
