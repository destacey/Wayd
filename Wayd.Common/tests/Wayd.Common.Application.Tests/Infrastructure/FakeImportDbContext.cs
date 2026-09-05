using Microsoft.EntityFrameworkCore;
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
public sealed class FakeImportDbContext : IImportDbContext
{
    private readonly List<ImportProcess> _importProcesses = [];
    private readonly List<ImportProcessRow> _importProcessRows = [];

    public DbSet<ImportProcess> ImportProcesses => _importProcesses.AsDbSet();
    public DbSet<ImportProcessRow> ImportProcessRows => _importProcessRows.AsDbSet();

    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }

    public void AddImportProcess(ImportProcess process)
    {
        _importProcesses.Add(process);
        _importProcessRows.AddRange(process.Rows);
    }
}
