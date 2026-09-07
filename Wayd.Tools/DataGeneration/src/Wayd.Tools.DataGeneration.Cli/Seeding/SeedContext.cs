using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// What every area reads from and writes to as a seed runs: the generated model, the ids the environment
/// has handed back so far, and the client to post through.
/// </summary>
/// <remarks>
/// This is the seam that lets an area be a unit. An area reads what it needs, generates its rows, posts
/// them, and publishes the ids it created — so the same area code serves a seed into an empty environment
/// and, later, an add into a populated one. Nothing here assumes the environment started empty.
/// </remarks>
public sealed class SeedContext(WaydSeedClient client, Action<string> log)
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, Guid>> _idsByArea = new(StringComparer.OrdinalIgnoreCase);

    public WaydSeedClient Client { get; } = client;

    public Action<string> Log { get; } = log;

    /// <summary>The generated organization. Set by the org generation step before any area runs.</summary>
    public GeneratedOrg Org { get; set; } = default!;

    /// <summary>
    /// The generated PPM model, keyed by the generator's own handles — portfolio names, project keys.
    /// Null when the seed was asked to skip PPM.
    /// </summary>
    /// <remarks>
    /// The model is deliberately name-keyed rather than id-keyed. Names are what the generator can decide
    /// for itself, and are what its own cross-references are built from; ids exist only once the API has
    /// created something, so they appear at the CSV boundary and nowhere earlier.
    /// </remarks>
    public GeneratedPpm? Ppm { get; set; }

    /// <summary>Expenditure category ids by name, from the settings bootstrap.</summary>
    public IReadOnlyDictionary<string, int> ExpenditureCategoryIds { get; set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The active project lifecycle every generated project is assigned.</summary>
    public Guid ProjectLifecycleId { get; set; }

    /// <summary>
    /// Records what an area created, so later areas can reference it.
    /// </summary>
    /// <remarks>
    /// Keyed by the import id each row was submitted under, which the generator sets to its own handle for
    /// that record — so a later area asks for the portfolio named "Payments" and gets the id the API
    /// assigned it, with no second mapping to keep in step.
    /// </remarks>
    public void Publish(string area, IReadOnlyDictionary<string, Guid> createdIds) => _idsByArea[area] = createdIds;

    /// <summary>The id of one record an earlier area created, by that area's name and the record's handle.</summary>
    public Guid Id(string area, string handle)
    {
        if (!_idsByArea.TryGetValue(area, out var ids))
            throw new SeedException($"Area '{area}' has not run, so '{handle}' cannot be resolved. Check the area's declared dependencies.");

        return ids.TryGetValue(handle, out var id)
            ? id
            : throw new SeedException($"Area '{area}' created no record for '{handle}'.");
    }

    /// <summary>Every id an earlier area created, for the handles given.</summary>
    public IReadOnlyList<Guid> Ids(string area, IEnumerable<string> handles) => [.. handles.Select(h => Id(area, h))];
}
