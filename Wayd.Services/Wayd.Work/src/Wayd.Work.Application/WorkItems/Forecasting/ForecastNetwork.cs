using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Application.WorkItems.Forecasting;

/// <summary>
/// The items a forecast has to simulate: its targets, every open predecessor up their
/// dependency chains, and each one's position in its team's backlog.
/// </summary>
internal sealed class ForecastNetwork
{
    public ForecastNetwork(
        IReadOnlyDictionary<Guid, ForecastNetworkItem> items,
        IReadOnlyList<ForecastDependency<Guid>> dependencies,
        IReadOnlyList<(ForecastNetworkItem Predecessor, ForecastNetworkItem Successor)> removedPredecessorDependencies,
        IReadOnlyDictionary<Guid, int> backlogPositions)
    {
        Items = items;
        Dependencies = dependencies;
        RemovedPredecessorDependencies = removedPredecessorDependencies;
        BacklogPositions = backlogPositions;
    }

    public IReadOnlyDictionary<Guid, ForecastNetworkItem> Items { get; }

    public IReadOnlyList<ForecastDependency<Guid>> Dependencies { get; }

    /// <summary>
    /// Active dependencies on a predecessor that was removed without being completed. They hold
    /// nothing up, so they are not in <see cref="Dependencies"/>, but they are worth reporting:
    /// the link probably needs cleaning up.
    /// </summary>
    public IReadOnlyList<(ForecastNetworkItem Predecessor, ForecastNetworkItem Successor)> RemovedPredecessorDependencies { get; }

    /// <summary>
    /// 1-based position in the team's backlog, for each open backlog item with a team. The
    /// position counts the item itself and everything ranked ahead of it.
    /// </summary>
    public IReadOnlyDictionary<Guid, int> BacklogPositions { get; }
}
