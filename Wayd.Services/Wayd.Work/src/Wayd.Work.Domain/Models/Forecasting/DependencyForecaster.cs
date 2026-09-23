using Ardalis.GuardClauses;

namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// Applies dependencies to completion forecasts: in each trial, a successor finishes no
/// earlier than the latest of its predecessors, through the whole chain.
/// </summary>
/// <remarks>
/// A successor is assumed able to finish the same day its last predecessor does, and a team
/// waiting on a blocked item is assumed to keep finishing the items behind it. Both lean
/// optimistic.
/// </remarks>
public static class DependencyForecaster
{
    /// <param name="ownForecasts">
    /// Every item in the network, including each predecessor, mapped to the forecast from its
    /// own backlog position — or null when it has none (no team, no rank, no history). Items on
    /// the same team must come from one <see cref="MonteCarloForecaster.ForecastBacklogPositions"/>
    /// call. Done predecessors should be left out, with their dependencies.
    /// </param>
    /// <param name="dependencies">
    /// Where these form a cycle, the dependency that closes it is ignored (see
    /// <see cref="DependencyForecast{TKey}.IgnoredDependencies"/>). Chains are followed from
    /// items with no predecessors first, then in the order the dependencies are given, so for
    /// a → b → c → a listed in that order it is c → a that is ignored.
    /// </param>
    public static DependencyForecast<TKey> Apply<TKey>(
        IReadOnlyDictionary<TKey, CompletionForecast?> ownForecasts,
        IEnumerable<ForecastDependency<TKey>> dependencies) where TKey : notnull
    {
        Guard.Against.Null(ownForecasts);
        Guard.Against.Null(dependencies);

        var edges = dependencies.Distinct().ToList();
        foreach (var edge in edges)
        {
            if (!ownForecasts.ContainsKey(edge.Predecessor) || !ownForecasts.ContainsKey(edge.Successor))
                throw new ArgumentException($"Dependency {edge.Predecessor} → {edge.Successor} names an item with no entry in {nameof(ownForecasts)}.", nameof(dependencies));
        }

        CompletionForecast.EnsureComparable(ownForecasts.Values.OfType<CompletionForecast>(), nameof(ownForecasts));

        var (order, kept, ignored) = BreakCycles(ownForecasts.Keys, edges);

        var predecessors = ownForecasts.Keys.ToDictionary(k => k, _ => new List<TKey>());
        foreach (var edge in kept)
            predecessors[edge.Successor].Add(edge.Predecessor);

        var items = new Dictionary<TKey, DependencyAdjustedForecast<TKey>>();
        foreach (var item in order)
        {
            var blockedBy = new List<TKey>();
            foreach (var predecessor in predecessors[item])
            {
                if (ownForecasts[predecessor] is null)
                    blockedBy.Add(predecessor);
                blockedBy.AddRange(items[predecessor].BlockedBy);
            }
            blockedBy = [.. blockedBy.Distinct()];

            var own = ownForecasts[item];
            var forecast = own is null || blockedBy.Count > 0 ? null
                : predecessors[item].Count == 0 ? own
                : CompletionForecast.LatestOf([own, .. predecessors[item].Select(p => items[p].Forecast!)]);

            items[item] = new DependencyAdjustedForecast<TKey>(item, forecast, blockedBy);
        }

        var influences = kept
            .Select(edge => new DependencyInfluence<TKey>(
                edge.Predecessor,
                edge.Successor,
                ShareOfTrialsSettingFinish(items[edge.Predecessor].Forecast, items[edge.Successor].Forecast, ownForecasts[edge.Successor])))
            .ToList();

        return new DependencyForecast<TKey>(items, influences, ignored);
    }

    private static double? ShareOfTrialsSettingFinish(
        CompletionForecast? predecessor,
        CompletionForecast? successor,
        CompletionForecast? successorOwn)
    {
        if (predecessor is null || successor is null || successorOwn is null)
            return null;

        var trials = 0;
        for (var trial = 0; trial < successor.Trials; trial++)
        {
            if (predecessor[trial] == successor[trial] && predecessor[trial] > successorOwn[trial])
                trials++;
        }

        return (double)trials / successor.Trials;
    }

    /// <summary>
    /// Walks each chain depth-first from predecessor to successor. A dependency leading back to
    /// an item already on the chain being walked closes a cycle and is ignored.
    /// </summary>
    /// <returns>
    /// Every item ordered so each follows its predecessors under the kept dependencies.
    /// </returns>
    private static (List<TKey> Order, List<ForecastDependency<TKey>> Kept, List<ForecastDependency<TKey>> Ignored) BreakCycles<TKey>(
        IEnumerable<TKey> items,
        List<ForecastDependency<TKey>> edges) where TKey : notnull
    {
        var successors = items.ToDictionary(k => k, _ => new List<ForecastDependency<TKey>>());
        foreach (var edge in edges)
            successors[edge.Predecessor].Add(edge);

        var hasPredecessor = edges.Select(e => e.Successor).ToHashSet();
        var inDependencyOrder = edges.SelectMany(e => new[] { e.Predecessor, e.Successor }).Distinct();
        var starts = inDependencyOrder.Where(i => !hasPredecessor.Contains(i))
            .Concat(inDependencyOrder)
            .Concat(successors.Keys);

        var onChain = new HashSet<TKey>();
        var finished = new HashSet<TKey>();
        var postOrder = new List<TKey>();
        var kept = new List<ForecastDependency<TKey>>();
        var ignored = new List<ForecastDependency<TKey>>();

        // An explicit stack rather than recursion: a chain as long as the call stack is deep
        // would end the process with an uncatchable stack overflow. Each frame is an item on
        // the chain and the index of the next of its dependencies to follow.
        var chain = new Stack<(TKey Item, int Next)>();
        foreach (var start in starts)
        {
            if (finished.Contains(start))
                continue;

            onChain.Add(start);
            chain.Push((start, 0));
            while (chain.TryPop(out var frame))
            {
                var itemSuccessors = successors[frame.Item];
                if (frame.Next == itemSuccessors.Count)
                {
                    onChain.Remove(frame.Item);
                    finished.Add(frame.Item);
                    postOrder.Add(frame.Item);
                    continue;
                }

                chain.Push((frame.Item, frame.Next + 1));
                var edge = itemSuccessors[frame.Next];
                if (onChain.Contains(edge.Successor))
                {
                    ignored.Add(edge);
                    continue;
                }

                kept.Add(edge);
                if (!finished.Contains(edge.Successor))
                {
                    onChain.Add(edge.Successor);
                    chain.Push((edge.Successor, 0));
                }
            }
        }

        postOrder.Reverse();
        return (postOrder, kept, ignored);
    }
}
