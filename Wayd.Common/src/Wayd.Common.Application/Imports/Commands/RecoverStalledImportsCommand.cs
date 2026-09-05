using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>What one sweep reclaimed.</summary>
public sealed record StalledImportRecovery(int Republished, int Failed);

/// <summary>
/// Reclaims runs no worker is going to finish.
/// </summary>
/// <remarks>
/// Nothing else can: a redelivery of the original message finds the run already claimed and stops, which is
/// exactly the guard that makes at-least-once delivery safe. A run whose worker died mid-chunk therefore
/// stays Processing forever unless something outside the run ends it.
/// </remarks>
public sealed record RecoverStalledImportsCommand : ICommand<StalledImportRecovery>, ILongRunningRequest;

public sealed class RecoverStalledImportsCommandHandler(
    IImportDbContext importDbContext,
    IDateTimeProvider dateTimeProvider,
    IDispatcher dispatcher,
    ILogger<RecoverStalledImportsCommandHandler> logger) : ICommandHandler<RecoverStalledImportsCommand, StalledImportRecovery>
{
    /// <summary>How long a run may sit Queued before the sweep assumes its message never arrived.</summary>
    private static readonly Duration _queuedGrace = Duration.FromMinutes(15);

    /// <summary>
    /// How long a running import may go without a heartbeat before it counts as dead. The runner writes one
    /// per chunk, so this bounds the slowest single chunk, not the slowest import.
    /// </summary>
    private static readonly Duration _progressGrace = Duration.FromMinutes(30);

    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<RecoverStalledImportsCommandHandler> _logger = logger;

    public async Task<Result<StalledImportRecovery>> Handle(
        RecoverStalledImportsCommand command, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        var republished = await RepublishAbandonedQueue(now - _queuedGrace, cancellationToken);
        var failed = await FailSilentRuns(now, now - _progressGrace, cancellationToken);

        return Result.Success(new StalledImportRecovery(republished, failed));
    }

    /// <summary>
    /// Sends a run message again for anything still Queued long after it was submitted. Safe to repeat: a
    /// run can only be claimed from Queued, so if the original message does turn up, whichever arrives
    /// second finds the run already claimed and stops.
    /// </summary>
    private async Task<int> RepublishAbandonedQueue(Instant cutoff, CancellationToken cancellationToken)
    {
        var ids = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Status == ImportProcessStatus.Queued && p.SubmittedOn < cutoff)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            _logger.LogWarning("Import {ImportProcessId} is still queued well after submission; publishing it again.", id);
            await _dispatcher.Publish(new RunImportProcessCommand(id), cancellationToken);
        }

        return ids.Count;
    }

    /// <summary>
    /// Fails runs that stopped reporting progress, rather than requeuing them. A silent worker cannot be
    /// distinguished from a dead one, and reapplying rows the first is still working through would double
    /// them. The unapplied rows stay Pending, so a person can resume the run once they know what happened.
    /// </summary>
    private async Task<int> FailSilentRuns(Instant now, Instant cutoff, CancellationToken cancellationToken)
    {
        var stalled = await _importDbContext.ImportProcesses
            .Where(p => (p.Status == ImportProcessStatus.Processing || p.Status == ImportProcessStatus.Cancelling)
                && p.LastProgressOn != null
                && p.LastProgressOn < cutoff)
            .ToListAsync(cancellationToken);

        if (stalled.Count == 0)
            return 0;

        foreach (var process in stalled)
        {
            process.Fail(
                "The worker applying this import stopped responding. Rows that were applied are unchanged; the rest can be resumed.",
                now);

            _logger.LogError(
                "Import {ImportProcessId} stalled after {Succeeded} of {Total} rows (last progress {LastProgressOn}); marking it failed.",
                process.Id, process.SucceededRowCount, process.TotalRowCount, process.LastProgressOn);
        }

        await _importDbContext.SaveChangesAsync(cancellationToken);

        return stalled.Count;
    }
}
