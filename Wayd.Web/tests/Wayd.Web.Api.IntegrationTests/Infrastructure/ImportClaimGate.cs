using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Holds an import run at Queued: when armed, the save in which a worker claims a run waits until the test
/// opens the gate, so a submission's response wait is certain to run out before the run can finish.
/// </summary>
/// <remarks>
/// Registered on the shared host for every test, so it does nothing until <see cref="Hold"/> is called.
/// Dispose the handle <see cref="Hold"/> returns in a <c>finally</c>: a worker left waiting would keep the
/// host from shutting down.
/// </remarks>
public sealed class ImportClaimGate : SaveChangesInterceptor
{
    // A ceiling on the wait, so a test that fails before opening the gate cannot hang the worker.
    private static readonly TimeSpan _maxHold = TimeSpan.FromSeconds(60);

    private TaskCompletionSource? _gate;

    public IDisposable Hold()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gate = gate;

        return new Handle(this, gate);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_gate is { } gate
            && eventData.Context is not null
            && eventData.Context.ChangeTracker.Entries<ImportProcess>().Any(IsClaim))
        {
            await gate.Task.WaitAsync(_maxHold, cancellationToken);
        }

        return result;
    }

    private static bool IsClaim(EntityEntry<ImportProcess> entry) =>
        entry.State == EntityState.Modified
        && entry.Entity.Status == ImportProcessStatus.Processing
        && entry.Property(p => p.Status).OriginalValue == ImportProcessStatus.Queued;

    private sealed class Handle(ImportClaimGate owner, TaskCompletionSource gate) : IDisposable
    {
        private readonly ImportClaimGate _owner = owner;
        private readonly TaskCompletionSource _gate = gate;

        public void Dispose()
        {
            Interlocked.CompareExchange(ref _owner._gate, null, _gate);
            _gate.TrySetResult();
        }
    }
}
