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

    public virtual string? BatchedImport => null;

    public abstract bool ShouldRun(SeedContext context);

    public abstract Task Run(SeedContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Splits rows into files within the row cap the server publishes for <see cref="BatchedImport"/>.
    /// </summary>
    protected IReadOnlyList<IReadOnlyList<TRow>> Batch<TRow, TKey>(
        SeedContext context, IEnumerable<TRow> rows, Func<TRow, TKey> groupBy)
        where TKey : notnull
    {
        var importKey = BatchedImport
            ?? throw new InvalidOperationException($"Area '{Name}' batches its rows but declares no {nameof(BatchedImport)}.");

        return Batch(rows, groupBy, context.ImportLimits.MaxRows(importKey));
    }

    /// <summary>
    /// Splits rows into files of at most <paramref name="maxRowsPerFile"/> rows, keeping every row of a
    /// group together.
    /// </summary>
    /// <remarks>
    /// The group is not a convenience: it is the same unit the import itself applies by. A child task names
    /// its parent by that parent's ImportId in the same file and the per-project task number advances as
    /// rows are applied, so the import keeps a project's rows together and so must a file. Cutting a group
    /// across two files hands the second one a child whose parent it never saw.
    /// <para>
    /// A single group larger than the cap is left whole and over the limit. Splitting it would break the
    /// references it exists to hold, so the import rejecting the file is the honest outcome.
    /// </para>
    /// </remarks>
    protected static IReadOnlyList<IReadOnlyList<TRow>> Batch<TRow, TKey>(
        IEnumerable<TRow> rows, Func<TRow, TKey> groupBy, int maxRowsPerFile)
        where TKey : notnull
    {
        List<IReadOnlyList<TRow>> batches = [];
        List<TRow> current = [];

        foreach (var group in rows.GroupBy(groupBy))
        {
            var members = group.ToList();

            // Checked before the group is added, so a file only passes the cap when one group alone does.
            if (current.Count > 0 && current.Count + members.Count > maxRowsPerFile)
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
    /// Posts each batch as its own run and gathers what they created. A failure names the batch it happened
    /// in, and the seed stops there rather than building the next stage on a partial one.
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
