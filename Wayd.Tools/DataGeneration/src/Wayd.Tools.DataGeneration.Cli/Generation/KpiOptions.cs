namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Knobs for the generated KPIs. Layered over the organization: every delivery team carries some, every ART a
/// couple, each with a monthly history over the timeline.
/// </summary>
public sealed class KpiOptions
{
    /// <summary>How many KPIs each delivery team carries. Its ART carries half as many, at least one.</summary>
    public int KpisPerTeam { get; init; } = 3;
}
