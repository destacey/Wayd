using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>How a resume left the run.</summary>
/// <param name="QueuedRowCount">Rows that will be attempted.</param>
/// <param name="SkippedRowCount">Rows that cannot be, because the retention sweep took their data.</param>
public sealed record ResumedImport(int QueuedRowCount, int SkippedRowCount);

/// <summary>
/// Puts a finished run back on the queue to attempt what it did not apply.
/// </summary>
/// <remarks>
/// One command behind two endpoints, because resuming a cancelled run and retrying its rejected rows differ
/// only in which rows are reset. Succeeded rows are never included: their payload is gone and the record
/// they created still exists, so reapplying one would duplicate it.
/// </remarks>
public sealed record ResumeImportProcessCommand(Guid ImportProcessId, bool RetryFailedRows = false)
    : ICommand<ResumedImport>;

public sealed class ResumeImportProcessCommandHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    IDispatcher dispatcher,
    ILogger<ResumeImportProcessCommandHandler> logger) : ICommandHandler<ResumeImportProcessCommand, ResumedImport>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<ResumeImportProcessCommandHandler> _logger = logger;

    public async Task<Result<ResumedImport>> Handle(ResumeImportProcessCommand command, CancellationToken cancellationToken)
    {
        var process = await _importDbContext.ImportProcesses
            .Include(p => p.Rows)
            .FirstOrDefaultAsync(p => p.Id == command.ImportProcessId, cancellationToken);

        if (process is null)
            return Result.Failure<ResumedImport>($"Import process '{command.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveForManage(
            _registry, _currentPrincipal, process.ImportType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<ResumedImport>(definition.Error);

        if (!process.IsTerminal)
            return Result.Failure<ResumedImport>($"This import is {process.Status}; wait for it to finish before resuming it.");

        var skipped = ResetRows(process, command.RetryFailedRows);

        // Counted after the reset, so this covers both the rows just returned to Pending and the ones a
        // stalled run never reached, which were Pending already and needed no reset.
        var queued = process.Rows.Count(r => r.Status == ImportRowStatus.Pending);

        if (queued == 0)
        {
            return Result.Failure<ResumedImport>(skipped > 0
                ? "Every remaining row has passed its retention window and can no longer be applied."
                : "There is nothing left to apply on this import.");
        }

        var requeue = process.Requeue(_dateTimeProvider.Now);
        if (requeue.IsFailure)
            return Result.Failure<ResumedImport>(requeue.Error);

        try
        {
            await _importDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else resumed it first. Publishing anyway would be harmless — only one delivery can
            // claim it — but the caller would be told their resume queued rows it did not.
            return Result.Failure<ResumedImport>("The import changed while it was being resumed. Refresh it and try again.");
        }
        // Attributed to whoever submitted the file, not whoever pressed the button. The rows this run
        // applies are their import; an admin retrying it should not end up as the author of the records.
        await _dispatcher.Publish(
            new RunImportProcessCommand(process.Id), process.SubmittedByUserId, cancellationToken);

        _logger.LogInformation(
            "Import {ImportProcessId} was requeued with {QueuedRowCount} row(s) to attempt ({SkippedRowCount} skipped).",
            process.Id, queued, skipped);

        return Result.Success(new ResumedImport(queued, skipped));
    }

    /// <summary>
    /// Returns the eligible rows to Pending and reports how many could not be. A row whose payload the
    /// retention sweep emptied refuses the reset and is counted as skipped rather than failing the whole
    /// call — the rest of the run can still be attempted, and the caller is told how much of it cannot.
    /// </summary>
    private static int ResetRows(ImportProcess process, bool retryFailedRows)
    {
        var skipped = 0;

        foreach (var row in process.Rows)
        {
            var eligible = row.Status == ImportRowStatus.Cancelled
                || (retryFailedRows && row.Status == ImportRowStatus.Failed);

            if (eligible && row.Reset().IsFailure)
                skipped++;
        }

        return skipped;
    }
}
