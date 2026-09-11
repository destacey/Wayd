using System.Diagnostics;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>
/// Applies a queued import. Carries only the id: the rows live in the database, and reading them from
/// there rather than from the message is what lets a redelivery, a resume and a retry all be the same
/// operation over a different set of Pending rows.
/// </summary>
public sealed record RunImportProcessCommand(Guid ImportProcessId) : ICommand, ILongRunningRequest;

public sealed class RunImportProcessCommandHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    IDateTimeProvider dateTimeProvider,
    ILogger<RunImportProcessCommandHandler> logger) : ICommandHandler<RunImportProcessCommand>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RunImportProcessCommandHandler> _logger = logger;

    public async Task<Result> Handle(RunImportProcessCommand command, CancellationToken cancellationToken)
    {
        var process = await _importDbContext.ImportProcesses
            .Include(p => p.Rows)
            .FirstOrDefaultAsync(p => p.Id == command.ImportProcessId, cancellationToken);

        if (process is null)
            return Result.Failure($"Import process '{command.ImportProcessId}' was not found.");

        var definitionResult = _registry.Find(process.ImportType);
        if (definitionResult.IsFailure)
            return await FailRun(process, definitionResult.Error, cancellationToken);

        // Claiming the run is what makes an at-least-once redelivery safe. A second delivery finds it
        // already claimed and stops here rather than reapplying rows.
        var claim = process.Start(Activity.Current?.TraceId.ToString() ?? command.ImportProcessId.ToString(), _dateTimeProvider.Now);
        if (claim.IsFailure)
        {
            _logger.LogInformation(
                "Import {ImportProcessId} was already {Status}; ignoring this delivery.", process.Id, process.Status);
            return Result.Success();
        }

        await _importDbContext.SaveChangesAsync(cancellationToken);

        var definition = definitionResult.Value;
        var stoppedEarly = false;
        var committedWork = false;

        try
        {
            for (var passIndex = 0; passIndex < definition.Passes.Count && !stoppedEarly; passIndex++)
            {
                var pass = definition.Passes[passIndex];
                var eligible = process.Rows.Where(r => r.Status == ImportRowStatus.Pending).ToList();

                if (eligible.Count == 0)
                    break;

                // An atomic import is never split, whatever its passes declare: discarding staged work only
                // holds while none of it has been saved, and a second chunk would mean the first already had.
                List<IReadOnlyList<ImportProcessRow>> chunks =
                    pass.Scope == ImportPassScope.WholeSet || definition.Atomicity == ImportAtomicity.Atomic
                        ? [eligible]
                        : Chunk(eligible, definition.ChunkSize);

                for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    // Read the status back rather than trusting the tracked instance: a cancellation arrives on
                    // a different request, in a different transaction, and would be invisible here otherwise.
                    if (await IsCancellationRequested(process.Id, cancellationToken))
                    {
                        stoppedEarly = true;
                        break;
                    }

                    var isFinalChunk = chunkIndex == chunks.Count - 1;
                    var passResult = await definition.ExecutePass(
                        process.Id, passIndex, chunks[chunkIndex], isFinalChunk, cancellationToken);

                    if (passResult.IsFailure)
                        return await FailRun(process, $"Pass '{pass.Name}' could not run: {passResult.Error}", cancellationToken);

                    var failedInChunk = ApplyOutcomes(process, chunks[chunkIndex], passResult.Value);

                    if (definition.Atomicity == ImportAtomicity.Atomic && failedInChunk > 0)
                    {
                        DiscardStagedChanges();
                        return await FailAtomicRun(process, pass.Name, cancellationToken);
                    }

                    // One SaveChanges per chunk covers the pass's entity changes and the row state together, so
                    // a row never names a record that was not saved. The rows stay Pending until the run
                    // completes, though, so from here on a failed attempt can no longer be released to retry.
                    process.RecordProgress(succeeded: 0, failed: failedInChunk, _dateTimeProvider.Now);
                    await _importDbContext.SaveChangesAsync(cancellationToken);
                    committedWork = true;
                    DetachAppliedEntities();
                }
            }

            return stoppedEarly
                ? await CancelRun(process, cancellationToken)
                : await CompleteRun(process, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Import {ImportProcessId} failed unexpectedly on attempt {Attempt} of {MaxAttempts}.",
                process.Id, process.AttemptCount, ImportProcess.MaxAttempts);

            // Rethrown only when released, so the failure policy's retry is what delivers the next attempt.
            // A run that ends here instead has recorded its outcome, and a retry would find nothing to do.
            if (await Abandon(process.Id, committedWork))
                throw;

            return Result.Success();
        }
    }

    /// <summary>
    /// Settles a run whose attempt threw: released for another attempt if it saved nothing and has attempts
    /// left, otherwise ended. Returns whether it was released.
    /// </summary>
    /// <remarks>
    /// Without this the run would stay Processing, and every retry of the message would stop at the claim
    /// as if another worker held it.
    /// <para>
    /// Nothing the failed attempt tracked can be trusted — its rows may name records that were never saved
    /// — so the tracker is cleared and the run read back as the database has it. The writes ignore the
    /// attempt's cancellation token: that token may be the reason for the failure, during a shutdown, and
    /// this write is what lets the next boot claim the run again.
    /// </para>
    /// </remarks>
    private async Task<bool> Abandon(Guid importProcessId, bool committedWork)
    {
        _importDbContext.ChangeTracker.Clear();

        var process = await _importDbContext.ImportProcesses
            .Include(p => p.Rows)
            .FirstAsync(p => p.Id == importProcessId, CancellationToken.None);

        if (process.Status == ImportProcessStatus.Cancelling)
        {
            await CancelRun(process, CancellationToken.None);
            return false;
        }

        if (!committedWork && process.Release(_dateTimeProvider.Now).IsSuccess)
        {
            await _importDbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(
                "Import {ImportProcessId} saved nothing before failing; released for attempt {NextAttempt} of {MaxAttempts}.",
                process.Id, process.AttemptCount + 1, ImportProcess.MaxAttempts);
            return true;
        }

        var now = _dateTimeProvider.Now;

        // As a cancellation does: a row that saved its record is settled as applied, so a resume picks up
        // only the rows never reached instead of creating the rest a second time.
        var applied = 0;
        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending && r.CreatedEntityId is not null))
        {
            row.MarkSucceeded(row.CreatedEntityId, now);
            applied++;
        }

        process.RecordProgress(applied, failed: 0, now);

        var reference = process.LastAttemptCorrelationId is { } traceId ? $" Reference: {traceId}." : string.Empty;
        process.Fail(
            committedWork
                ? $"An unexpected error stopped this import partway through. Rows it had already applied are unchanged, and the rest can be resumed.{reference}"
                : $"An unexpected error stopped this import on each of {process.AttemptCount} attempts. Nothing was applied.{reference}",
            now);
        await _importDbContext.SaveChangesAsync(CancellationToken.None);

        return false;
    }

    /// <summary>
    /// Writes each row's outcome and returns how many this call rejected. A row is only marked succeeded
    /// once every pass has run — an employee created by the first pass is not finished until the manager
    /// and deactivation passes have had their turn.
    /// </summary>
    private int ApplyOutcomes(ImportProcess process, IReadOnlyList<ImportProcessRow> rows, ImportPassResult result)
    {
        var byImportId = rows.ToDictionary(r => r.ImportId, StringComparer.Ordinal);
        var failed = 0;
        var now = _dateTimeProvider.Now;

        foreach (var outcome in result.Rows)
        {
            if (!byImportId.TryGetValue(outcome.ImportId, out var row))
                continue;

            if (outcome.Failed)
            {
                row.MarkFailed(outcome.Error ?? "The row was rejected.", now);
                failed++;
                continue;
            }

            if (outcome.CreatedEntityId is { } createdEntityId)
                row.RecordCreatedEntity(createdEntityId);

            if (outcome.Warning is not null)
                row.RecordWarning(outcome.Warning);
        }

        return failed;
    }

    private async Task<bool> IsCancellationRequested(Guid importProcessId, CancellationToken cancellationToken)
    {
        var status = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Id == importProcessId)
            .Select(p => p.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status == ImportProcessStatus.Cancelling;
    }

    private async Task<Result> CompleteRun(ImportProcess process, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var succeeded = 0;

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending))
        {
            row.MarkSucceeded(row.CreatedEntityId, now);
            succeeded++;
        }

        process.RecordProgress(succeeded, failed: 0, now);
        process.Complete(now);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Import {ImportProcessId} finished as {Status}: {Succeeded} applied, {Failed} rejected.",
            process.Id, process.Status, process.SucceededRowCount, process.FailedRowCount);

        return Result.Success();
    }

    /// <summary>
    /// Stops on request. Rows already applied stay applied; the ones never reached are marked cancelled so
    /// they are distinguishable from rows that were tried and rejected, and so Resume can pick them up.
    /// </summary>
    private async Task<Result> CancelRun(ImportProcess process, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var succeeded = 0;

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending))
        {
            if (row.CreatedEntityId is null)
            {
                row.MarkCancelled(now);
                continue;
            }

            row.MarkSucceeded(row.CreatedEntityId, now);
            succeeded++;
        }

        process.RecordProgress(succeeded, failed: 0, now);
        process.Cancel(now);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Import {ImportProcessId} stopped on request after {Succeeded} row(s).", process.Id, process.SucceededRowCount);

        return Result.Success();
    }

    /// <summary>
    /// Lets go of the records a chunk applied, now that they are saved.
    /// </summary>
    /// <remarks>
    /// Every module interface resolves to the one context in this message, so without this a fifty
    /// thousand row import would hold every record it created for the life of the run. The chunk is the
    /// natural boundary: it has just been saved, and nothing later needs it.
    /// <para>
    /// Which is also the constraint this places on a definition — a pass may not rely on entities an
    /// earlier pass left tracked. Both multi-pass imports already query theirs back by natural key, which
    /// is what makes that reasonable to ask.
    /// </para>
    /// <para>
    /// The run and its rows stay: the runner keeps writing to them, and completes them at the end.
    /// </para>
    /// </remarks>
    private void DetachAppliedEntities()
    {
        var applied = _importDbContext.ChangeTracker.Entries()
            .Where(e => e.Entity is not ImportProcess and not ImportProcessRow)
            .ToList();

        foreach (var entry in applied)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Throws away everything the pass staged, so an atomic run that rejects a row applies none of it.
    /// </summary>
    /// <remarks>
    /// A pass is asked to validate before it mutates, and that covers the rejections it makes itself — an
    /// unresolved reference, a parent of the wrong kind. It cannot cover the ones the domain raises
    /// <em>while</em> mutating: a cycle is only found when the edge closing it is added, by which point the
    /// earlier edges are staged. Detaching is enough because the check runs before the chunk is saved, and
    /// an atomic import is never split — so nothing it staged has reached the database.
    /// <para>
    /// The import's own rows are left tracked: they carry the outcome and are saved immediately after.
    /// </para>
    /// </remarks>
    private void DiscardStagedChanges()
    {
        var staged = _importDbContext.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not ImportProcess and not ImportProcessRow)
            .ToList();

        foreach (var entry in staged)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// An atomic import applies as one unit, so a single rejection keeps the whole file out.
    /// </summary>
    private async Task<Result> FailAtomicRun(ImportProcess process, string passName, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var notApplied = 0;

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending))
        {
            row.MarkFailed("Not applied: this import is all-or-nothing and another row was rejected.", now);
            notApplied++;
        }

        process.RecordProgress(succeeded: 0, failed: notApplied, now);
        process.Fail($"Pass '{passName}' rejected at least one row, and this import applies as one unit.", now);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> FailRun(ImportProcess process, string error, CancellationToken cancellationToken)
    {
        process.Fail(error, _dateTimeProvider.Now);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogError("Import {ImportProcessId} failed: {Error}", process.Id, error);

        return Result.Success();
    }

    private static List<IReadOnlyList<ImportProcessRow>> Chunk(List<ImportProcessRow> rows, int chunkSize) =>
        [.. rows.Chunk(Math.Max(1, chunkSize)).Select(c => (IReadOnlyList<ImportProcessRow>)c)];
}
