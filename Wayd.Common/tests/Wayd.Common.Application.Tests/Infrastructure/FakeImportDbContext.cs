using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Imports;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for <see cref="IImportDbContext"/>.
/// </summary>
/// <remarks>
/// The runner re-reads the run's status to notice a cancellation raised on another request. Here that read
/// returns the same in-memory instance, so a test simulates the cancellation by having a pass call
/// <c>RequestCancellation</c> — the next status read then sees it, exactly as a real one would.
/// </remarks>
public sealed class FakeImportDbContext : IImportDbContext, IDisposable
{
    private readonly List<ImportProcess> _importProcesses = [];
    private readonly List<ImportProcessRow> _importProcessRows = [];

    public DbSet<ImportProcess> ImportProcesses => _importProcesses.AsDbSet();
    public DbSet<ImportProcessRow> ImportProcessRows => _importProcessRows.AsDbSet();

    /// <summary>
    /// Nothing is tracked here, so an atomic discard has nothing to do — which is why the discard itself is
    /// asserted against a real provider rather than in these tests.
    /// </summary>
    public ChangeTracker ChangeTracker => _tracker.ChangeTracker;

    private readonly EmptyTrackerContext _tracker = new();

    public int SaveChangesCallCount { get; private set; }

    public void Dispose() => _tracker.Dispose();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }

    /// <summary>Supplies a real but empty ChangeTracker, so the runner's discard is a no-op here.</summary>
    private sealed class EmptyTrackerContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseInMemoryDatabase($"import-fake-{Guid.CreateVersion7()}");
    }

    public void AddImportProcess(ImportProcess process)
    {
        _importProcesses.Add(process);
        _importProcessRows.AddRange(process.Rows);
    }
}
