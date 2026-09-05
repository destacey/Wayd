namespace Wayd.Common.Application.Imports;

/// <summary>
/// What a pass is given: the rows it is responsible for this time round, and where they sit in the run.
/// </summary>
/// <remarks>
/// For a <see cref="ImportPassScope.Chunked"/> pass this is one chunk and the pass will be called again for
/// the next; for a <see cref="ImportPassScope.WholeSet"/> pass it is every row, once.
/// </remarks>
public sealed class ImportPassContext<TRow>
{
    internal ImportPassContext(
        Guid importProcessId,
        string passName,
        IReadOnlyList<ImportRowItem<TRow>> rows,
        bool isFinalChunk)
    {
        ImportProcessId = importProcessId;
        PassName = passName;
        Rows = rows;
        IsFinalChunk = isFinalChunk;
    }

    public Guid ImportProcessId { get; }

    public string PassName { get; }

    /// <summary>The rows this call covers. Never empty — the runner does not invoke a pass with nothing to do.</summary>
    public IReadOnlyList<ImportRowItem<TRow>> Rows { get; }

    /// <summary>
    /// True on the last chunk of this pass, so a pass that has to reconcile something once it has seen
    /// everything can do so without needing the whole set in memory at once.
    /// </summary>
    public bool IsFinalChunk { get; }

    /// <summary>Rows this call has not rejected — what a pass persists.</summary>
    public IEnumerable<ImportRowItem<TRow>> Accepted => Rows.Where(r => !r.IsFailed);
}
