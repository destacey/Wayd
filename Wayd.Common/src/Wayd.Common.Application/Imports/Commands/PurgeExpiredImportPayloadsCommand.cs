using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>
/// Drops the stored copy of every row belonging to a run that finished more than the retention window ago.
/// </summary>
/// <remarks>
/// A row's payload is a verbatim copy of what someone uploaded — names, emails, employee numbers — kept only
/// so a failed row can be retried. Once a run is old enough that nobody is going to retry it, the copy is
/// just a second place that data lives. The run and its per-row outcomes stay: they are the record of what
/// happened, and carry nothing that was not already imported.
/// </remarks>
public sealed record PurgeExpiredImportPayloadsCommand : ICommand<int>, ILongRunningRequest;

public sealed class PurgeExpiredImportPayloadsCommandHandler(
    IImportDbContext importDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<PurgeExpiredImportPayloadsCommandHandler> logger) : ICommandHandler<PurgeExpiredImportPayloadsCommand, int>
{
    private static readonly Duration _retention = Duration.FromDays(30);

    // Bounded so one sweep over a long-neglected table cannot load every row it has ever stored.
    private const int BatchSize = 500;

    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<PurgeExpiredImportPayloadsCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(PurgeExpiredImportPayloadsCommand command, CancellationToken cancellationToken)
    {
        var cutoff = _dateTimeProvider.Now - _retention;

        var purged = 0;

        while (true)
        {
            // CompletedOn is set only by the terminal transitions and cleared again by a requeue, so a run
            // that has one is finished — no status filter is needed to keep the sweep off a live import.
            var rows = await _importDbContext.ImportProcesses
                .Where(p => p.CompletedOn != null && p.CompletedOn < cutoff)
                .SelectMany(p => p.Rows)
                .Where(r => r.Payload != null)
                .OrderBy(r => r.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                row.PurgePayload();
            }

            await _importDbContext.SaveChangesAsync(cancellationToken);
            purged += rows.Count;

            // Each batch is done with once it is saved, and a neglected table can be many batches. Left
            // tracked they would accumulate for the whole sweep, so the last batch pays for every one
            // before it.
            DetachSavedRows();
        }

        if (purged > 0)
            _logger.LogInformation("Import retention sweep cleared {RowCount} row payload(s) older than {Retention}.", purged, _retention);

        return Result.Success(purged);
    }

    private void DetachSavedRows()
    {
        foreach (var entry in _importDbContext.ChangeTracker.Entries<ImportProcessRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
