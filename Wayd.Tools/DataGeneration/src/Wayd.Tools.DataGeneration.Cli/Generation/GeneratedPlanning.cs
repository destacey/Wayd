namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated Planning dataset, keyed throughout by the generator's own handles. Each seed area projects
/// its slice to CSV rows, substituting the ids the environment has handed back by then.
/// </summary>
public sealed record GeneratedPlanning(
    IReadOnlyList<PlanningIntervalModel> PlanningIntervals,
    IReadOnlyList<PlanningIntervalObjectiveModel> Objectives,
    IReadOnlyList<RiskModel> Risks);
