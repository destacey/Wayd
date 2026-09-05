using CSharpFunctionalExtensions;

namespace Wayd.Common.Application.Imports;

/// <summary>
/// Whether a pass may be split across chunks.
/// </summary>
public enum ImportPassScope
{
    /// <summary>
    /// Rows are independent of one another <em>given the earlier passes finished</em>, so the runner may
    /// split them. Most passes qualify: a pass that needs to see rows the previous pass created can query
    /// them back rather than hold them in memory.
    /// </summary>
    Chunked = 1,

    /// <summary>
    /// The pass must see every row at once. Reserved for the cases where correctness depends on it — the
    /// team-hierarchy import loads all referenced teams tracked together so the domain's cycle check sees
    /// intra-batch edges, and chunking would make that check blind to half the file.
    /// </summary>
    WholeSet = 2,
}

/// <summary>
/// One ordered step of an import.
/// </summary>
/// <remarks>
/// Imports are not row loops. The employee import creates every row manager-less, then resolves manager
/// links (a manager may be any row in the file or someone who already existed), then deactivates the
/// leavers — three steps with a hard ordering between them. Modelling that as passes is what lets the
/// runner chunk safely: it chunks <em>within</em> a pass, and never starts one before the last finished.
/// </remarks>
public sealed record ImportPass<TRow>(
    string Name,
    ImportPassScope Scope,
    Func<ImportPassContext<TRow>, CancellationToken, Task<Result>> Execute);
