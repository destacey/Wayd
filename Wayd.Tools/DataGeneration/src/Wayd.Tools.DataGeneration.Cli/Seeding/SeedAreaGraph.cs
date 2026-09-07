namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Orders areas so each one runs after everything it declares a dependency on.
/// </summary>
/// <remarks>
/// Separate from the runner so the ordering can be tested without an environment to seed into — the thing
/// most likely to go wrong as areas are added is the graph, not the posting.
/// </remarks>
public static class SeedAreaGraph
{
    /// <summary>
    /// Topologically orders the areas, and refuses a graph that cannot be satisfied.
    /// </summary>
    /// <remarks>
    /// Ties are broken by the order the areas were registered, so a run is reproducible and reads in the
    /// sequence someone would expect rather than in whatever order a hash landed.
    /// </remarks>
    public static IReadOnlyList<ISeedArea> Order(IReadOnlyList<ISeedArea> areas)
    {
        var byName = new Dictionary<string, ISeedArea>(StringComparer.OrdinalIgnoreCase);
        foreach (var area in areas)
        {
            if (!byName.TryAdd(area.Name, area))
                throw new SeedException($"Two seed areas are both named '{area.Name}'.");
        }

        var unknown = areas
            .SelectMany(a => a.DependsOn.Select(d => (Area: a.Name, Dependency: d)))
            .Where(pair => !byName.ContainsKey(pair.Dependency))
            .ToList();
        if (unknown.Count > 0)
        {
            var described = unknown.Select(u => $"'{u.Area}' depends on '{u.Dependency}'");
            throw new SeedException($"Seed areas declare dependencies that do not exist: {string.Join(", ", described)}.");
        }

        List<ISeedArea> ordered = new(areas.Count);
        HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);
        var remaining = areas.ToList();

        while (remaining.Count > 0)
        {
            var ready = remaining.Where(a => a.DependsOn.All(placed.Contains)).ToList();

            if (ready.Count == 0)
            {
                var cycle = string.Join(", ", remaining.Select(a => $"'{a.Name}'"));
                throw new SeedException($"Seed areas form a dependency cycle: {cycle}.");
            }

            foreach (var area in ready)
            {
                ordered.Add(area);
                placed.Add(area.Name);
                remaining.Remove(area);
            }
        }

        return ordered;
    }
}
