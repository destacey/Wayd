using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The import runner's own tables.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="IStatusWorkflowDbContext"/>:
/// every module's import handlers write row outcomes, so each module's test fake has to supply these tables
/// — and hanging them off the global interface would make that fake owe an implementation of the whole
/// thing, employees and providers and scoring models included, none of which an import handler touches.
/// </remarks>
public interface IImportDbContext
{
    DbSet<ImportProcess> ImportProcesses { get; }
    DbSet<ImportProcessRow> ImportProcessRows { get; }

    /// <summary>
    /// Lets the runner throw away what an atomic import staged. Its passes mutate before the domain checks
    /// that only fire while mutating — a cycle is discovered while adding the edge that closes it — so
    /// "validate before you mutate" cannot cover every rejection, and the run has to be able to discard
    /// what it did rather than promise it did nothing.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Raises the command timeout for the run, putting back the context's own ceiling when disposed.
    /// </summary>
    /// <remarks>
    /// An import is not a request: its size is bounded by the row cap, not by anyone waiting, and a chunk
    /// of a few hundred records with their audit trail and activity log is well past what a request-path
    /// write should ever be allowed. A run used to fail on the default ceiling with nothing to say but
    /// "Execution Timeout Expired", three attempts running.
    /// </remarks>
    IDisposable WithCommandTimeout(TimeSpan timeout);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens a transaction that is rolled back when the scope is disposed, and holds back every event raised
    /// until then, so a preflight can save what its later passes read back and leave nothing behind.
    /// </summary>
    /// <remarks>
    /// Holding events back is not optional. Durable events are handed to their queue as soon as they are
    /// saved, and their handlers commit on a connection of their own, so a rollback alone would still
    /// deliver them. Disposing the scope also clears the change tracker: whatever it tracked was saved
    /// inside the rolled-back transaction and no longer exists.
    /// </remarks>
    Task<IAsyncDisposable> BeginPreflight(CancellationToken cancellationToken);
}
