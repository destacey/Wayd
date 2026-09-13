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

    /// <summary>
    /// The most rows a seed puts in one file. The atomic imports reject anything past 10,000 outright,
    /// since an atomic run cannot be split server-side and the cap is what bounds it; this leaves room
    /// under that rather than sitting on it, because a batch is sized by whole groups and the last one
    /// added can overshoot a tighter limit.
    /// </summary>
    private const int MaxRowsPerFile = 8_000;

    /// <summary>
    /// Splits rows into files small enough for one import run, keeping every row of a group together.
    /// </summary>
    /// <remarks>
    /// The group is not a convenience: the imports that need batching are whole-set precisely because
    /// rows within a group are not independent — a child task names its parent by that parent's ImportId
    /// in the same file, and the per-project task number advances as rows are applied. Cutting a group
    /// across two files hands the second one a child whose parent it never saw.
    /// <para>
    /// A single group larger than the cap is left whole and over the limit. Splitting it would break the
    /// references it exists to hold, so the import rejecting the file is the honest outcome.
    /// </para>
    /// </remarks>
    protected static IReadOnlyList<IReadOnlyList<TRow>> Batch<TRow, TKey>(
        IEnumerable<TRow> rows, Func<TRow, TKey> groupBy)
        where TKey : notnull
    {
        List<IReadOnlyList<TRow>> batches = [];
        List<TRow> current = [];

        foreach (var group in rows.GroupBy(groupBy))
        {
            var members = group.ToList();

            if (current.Count > 0 && current.Count + members.Count > MaxRowsPerFile)
            {
                batches.Add(current);
                current = [];
            }

            current.AddRange(members);
        }

        if (current.Count > 0)
            batches.Add(current);

        return batches;
    }

    /// <summary>
    /// Posts each batch as its own run and gathers what they created. Each run is atomic, so a failure
    /// names the batch it happened in and nothing from that batch exists.
    /// </summary>
    protected static async Task<IReadOnlyDictionary<string, Guid>> ImportBatches<TRow>(
        SeedContext context,
        string label,
        IReadOnlyList<IReadOnlyList<TRow>> batches,
        Func<IReadOnlyList<TRow>, Task<ImportRun>> import)
    {
        Dictionary<string, Guid> created = new(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < batches.Count; i++)
        {
            if (batches.Count > 1)
                context.Log($"  batch {i + 1} of {batches.Count}: {batches[i].Count} {label}");

            var run = await import(batches[i]);
            foreach (var (importId, id) in run.CreatedIdsByImportId)
                created[importId] = id;
        }

        return created;
    }
}
