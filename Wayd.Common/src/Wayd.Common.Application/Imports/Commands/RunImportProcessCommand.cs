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

        try
        {
            await _importDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two deliveries read the run as Queued at once; the status check on the write let only the
            // other one claim it.
            _logger.LogInformation("Import {ImportProcessId} was claimed by another delivery; ignoring this one.", process.Id);
            return Result.Success();
        }

        var definition = definitionResult.Value;
        var lastPassIndex = definition.Passes.Count - 1;
        var stoppedEarly = false;

        try
        {
            for (var passIndex = 0; passIndex <= lastPassIndex && !stoppedEarly; passIndex++)
            {
                var pass = definition.Passes[passIndex];

                // Only the rows due this pass. An earlier attempt may have taken some of them further, and
                // the work it saved is in the database already.
                var eligible = process.Rows
                    .Where(r => r.Status == ImportRowStatus.Pending && r.CompletedPassCount == passIndex)
                    .ToList();

                if (eligible.Count == 0)
                    continue;

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
                    {
                        // Whatever the pass staged before giving up must not ride along on the save that
                        // records the failure.
                        DiscardStagedChanges();
                        return await FailRun(process, $"Pass '{pass.Name}' could not run: {passResult.Error}", cancellationToken);
                    }

                    // Checked before any outcome is written: an atomic chunk that rejects a row must record
                    // neither the progress nor the success of the rows it accepted.
                    if (definition.Atomicity == ImportAtomicity.Atomic && passResult.Value.Rows.Any(r => r.Failed))
                    {
                        DiscardStagedChanges();
                        var rejected = RecordRejections(chunks[chunkIndex], passResult.Value);
                        return await FailAtomicRun(process, pass.Name, rejected, cancellationToken);
                    }

                    var (succeededInChunk, failedInChunk) = ApplyOutcomes(
                        chunks[chunkIndex], passResult.Value, passIndex, isLastPass: passIndex == lastPassIndex);

                    // One SaveChanges per chunk covers the pass's entity changes and each row's progress
                    // together. Whether it commits or not, the database then agrees with itself about how far
                    // every row got — which is what any later attempt reads.
                    process.RecordProgress(succeededInChunk, failedInChunk, _dateTimeProvider.Now);
                    await _importDbContext.SaveChangesAsync(cancellationToken);
                    DetachAppliedEntities();
                }
            }

            return stoppedEarly
                ? await CancelRun(process, cancellationToken)
                : await CompleteRun(process, cancellationToken);
        }
        catch (Exception exception)
        {
            if (exception is DbUpdateConcurrencyException)
            {
                _logger.LogInformation(
                    "Import {ImportProcessId} was changed by another request mid-run; settling it as it now stands.",
                    process.Id);
            }
            else
            {
                _logger.LogError(
                    exception,
                    "Import {ImportProcessId} failed unexpectedly on attempt {Attempt} of {MaxAttempts}.",
                    process.Id, process.AttemptCount, ImportProcess.MaxAttempts);
            }

            // Rethrown only when released, so the failure policy's retry is what delivers the next attempt.
            // A run that ends here instead has recorded its outcome, and a retry would find nothing to do.
            if (await Abandon(process.Id))
                throw;

            return Result.Success();
        }
    }

    /// <summary>
    /// Settles a run whose attempt threw: released for another attempt while it has attempts left, otherwise
    /// ended. Returns whether it was released.
    /// </summary>
    /// <remarks>
    /// Without this the run would stay Processing, and every retry of the message would stop at the claim
    /// as if another worker held it. Releasing is safe however far the attempt got, because each row's
    /// progress was saved with the work it counts: the next attempt carries on from there.
    /// <para>
    /// Nothing the failed attempt tracked can be trusted — its rows may record progress that was never
    /// saved — so the tracker is cleared and the run read back as the database has it. The writes ignore
    /// the attempt's cancellation token: that token may be the reason for the failure, during a shutdown,
    /// and this write is what lets the next boot claim the run again.
    /// </para>
    /// </remarks>
    private async Task<bool> Abandon(Guid importProcessId)
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

        // The status is a concurrency token, so if a stop lands between the read above and this write, the
        // write fails rather than overwriting the stop with Queued.
        if (process.Release(_dateTimeProvider.Now).IsSuccess)
        {
            await _importDbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(
                "Import {ImportProcessId} released for attempt {NextAttempt} of {MaxAttempts}.",
                process.Id, process.AttemptCount + 1, ImportProcess.MaxAttempts);
            return true;
        }

        var reference = process.LastAttemptCorrelationId is { } traceId ? $" Reference: {traceId}." : string.Empty;
        process.Fail(
            $"An unexpected error stopped this import on each of {process.AttemptCount} attempts. Rows it had already applied are unchanged, and the rest can be resumed.{reference}",
            _dateTimeProvider.Now);
        await _importDbContext.SaveChangesAsync(CancellationToken.None);

        return false;
    }

    /// <summary>
    /// Writes each row's outcome for one pass and returns how many it finished and rejected. A row is only
    /// marked succeeded when its last pass saves — an employee created by the first pass is not finished
    /// until the manager and deactivation passes have had their turn.
    /// </summary>
    private (int Succeeded, int Failed) ApplyOutcomes(
        IReadOnlyList<ImportProcessRow> rows, ImportPassResult result, int passIndex, bool isLastPass)
    {
        var byImportId = rows.ToDictionary(r => r.ImportId, StringComparer.Ordinal);
        var succeeded = 0;
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

            row.RecordPassCompleted(passIndex);

            if (isLastPass)
            {
                row.MarkSucceeded(row.CreatedEntityId, now);
                succeeded++;
            }
        }

        return (succeeded, failed);
    }

    /// <summary>Marks only the rows the pass rejected, with their reasons, and returns how many.</summary>
    private int RecordRejections(IReadOnlyList<ImportProcessRow> rows, ImportPassResult result)
    {
        var byImportId = rows.ToDictionary(r => r.ImportId, StringComparer.Ordinal);
        var now = _dateTimeProvider.Now;
        var rejected = 0;

        foreach (var outcome in result.Rows.Where(o => o.Failed))
        {
            if (!byImportId.TryGetValue(outcome.ImportId, out var row))
                continue;

            row.MarkFailed(outcome.Error ?? "The row was rejected.", now);
            rejected++;
        }

        return rejected;
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

    /// <summary>Every row was settled as its last pass saved, so all that is left is the run itself.</summary>
    private async Task<Result> CompleteRun(ImportProcess process, CancellationToken cancellationToken)
    {
        process.Complete(_dateTimeProvider.Now);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Import {ImportProcessId} finished as {Status}: {Succeeded} applied, {Failed} rejected.",
            process.Id, process.Status, process.SucceededRowCount, process.FailedRowCount);

        return Result.Success();
    }

    /// <summary>
    /// Stops on request. Rows already applied stay applied; the rest are marked cancelled so they are
    /// distinguishable from rows that were tried and rejected, and so Resume can pick them up — from the
    /// pass each one reached, for a row that got partway.
    /// </summary>
    private async Task<Result> CancelRun(ImportProcess process, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending))
        {
            row.MarkCancelled(now);
        }

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
    private async Task<Result> FailAtomicRun(
        ImportProcess process, string passName, int rejected, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var notApplied = 0;

        foreach (var row in process.Rows.Where(r => r.Status == ImportRowStatus.Pending))
        {
            row.MarkFailed("Not applied: this import is all-or-nothing and another row was rejected.", now);
            notApplied++;
        }

        process.RecordProgress(succeeded: 0, failed: rejected + notApplied, now);
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
