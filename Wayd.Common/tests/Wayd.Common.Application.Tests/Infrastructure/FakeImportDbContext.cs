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

    /// <summary>Saves made while a preflight scope was open — the ones a real provider rolls back.</summary>
    public int PreflightSaveChangesCallCount { get; private set; }

    public bool IsPreflightOpen { get; private set; }

    public int PreflightsBegun { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (IsPreflightOpen)
            PreflightSaveChangesCallCount++;

        return Task.FromResult(0);
    }

    /// <summary>
    /// Records only that a scope was open. There is no transaction to roll back here, so what a preflight
    /// leaves behind is asserted against a real provider.
    /// </summary>
    public Task<IAsyncDisposable> BeginPreflight(CancellationToken cancellationToken)
    {
        IsPreflightOpen = true;
        PreflightsBegun++;
        return Task.FromResult<IAsyncDisposable>(new PreflightScope(this));
    }

    private sealed class PreflightScope(FakeImportDbContext context) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            context.IsPreflightOpen = false;
            return ValueTask.CompletedTask;
        }
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
