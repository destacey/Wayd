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
    /// <summary>
    /// How long one of a run's commands may take. Generous rather than tuned: the point is that a run is
    /// bounded by its row cap and not by a caller's patience, so the ceiling only has to be past anything a
    /// capped file can reach. The stall sweep, not this, is what ends a run that is genuinely stuck.
    /// </summary>
    private static readonly TimeSpan ImportCommandTimeout = TimeSpan.FromMinutes(10);

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

        // A chunk writes its records, their audit trail and their activity log in one transaction, and an
        // atomic import does the whole file that way. That is minutes of work on a large file, against a
        // ceiling meant for a request; without this the run fails on every attempt with nothing but
        // "Execution Timeout Expired" to say why.
        using var commandTimeout = _importDbContext.WithCommandTimeout(ImportCommandTimeout);

        try
        {
            if (process.IsPreflight)
                return await RunPreflight(process, definition, cancellationToken);

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

                var chunks = ChunksFor(definition, pass, eligible);

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
                    var passResult = await ExecuteChunk(
                        definition, process.Id, passIndex, chunks[chunkIndex], isFinalChunk, cancellationToken);

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
    /// Puts every row through the import's passes and applies none of them.
    /// </summary>
    /// <remarks>
    /// Only a pass that a later pass reads back is saved, inside a transaction rolled back at the end; the
    /// last pass is staged and thrown away. Saving it would add nothing a row could report — a save that
    /// fails fails the whole chunk, never one row — and would spend identity values, leaving a gap in the
    /// visible keys of every record the file would have created.
    /// <para>
    /// The passes run against copies of the rows, never the tracked ones. Their outcomes are written only
    /// after the rollback, so the run's own rows hold no locks the details page or a cancellation would wait
    /// on, and the progress a pass records against rolled-back work is never persisted.
    /// </para>
    /// <para>
    /// An atomic import does not stop at the first pass that rejects a row, as a real run must: a preflight
    /// exists to report every row, so the rows that were not rejected carry on through the later passes.
    /// </para>
    /// </remarks>
    private async Task<Result> RunPreflight(ImportProcess process, IImportDefinition definition, CancellationToken cancellationToken)
    {
        var rehearsal = process.Rows
            .Select(r => ImportProcessRow.Create(r.ImportId, r.RowNumber, r.Payload!, r.GroupKey))
            .ToList();

        var lastPassIndex = definition.Passes.Count - 1;
        var stoppedEarly = false;
        string? passFailure = null;

        await using (await _importDbContext.BeginPreflight(cancellationToken))
        {
            for (var passIndex = 0; passIndex <= lastPassIndex && !stoppedEarly && passFailure is null; passIndex++)
            {
                var pass = definition.Passes[passIndex];

                var eligible = rehearsal
                    .Where(r => r.Status == ImportRowStatus.Pending && r.CompletedPassCount == passIndex)
                    .ToList();
                if (eligible.Count == 0)
                    break;

                // Chunked, and a rejected group kept out, exactly as a real run would, so the preflight reports
                // the same rows the run would reject.
                var chunks = ChunksFor(definition, pass, eligible);

                for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    if (await IsCancellationRequested(process.Id, cancellationToken))
                    {
                        stoppedEarly = true;
                        break;
                    }

                    var passResult = await ExecuteChunk(
                        definition, process.Id, passIndex, chunks[chunkIndex], chunkIndex == chunks.Count - 1, cancellationToken);

                    if (passResult.IsFailure)
                    {
                        passFailure = $"Pass '{pass.Name}' could not run: {passResult.Error}";
                        break;
                    }

                    RecordRehearsalOutcomes(chunks[chunkIndex], passResult.Value, passIndex);

                    if (passIndex < lastPassIndex)
                        await _importDbContext.SaveChangesAsync(cancellationToken);

                    // Lets go of what was saved, and discards what the last pass staged.
                    _importDbContext.ChangeTracker.Clear();
                }
            }
        }

        // The scope cleared the tracker, so the run is read back as the database has it — which may no longer
        // be Processing, if the stall sweep gave up on a preflight that outlasted its grace period.
        var run = await _importDbContext.ImportProcesses
            .Include(p => p.Rows)
            .FirstAsync(p => p.Id == process.Id, cancellationToken);

        if (run.IsTerminal)
        {
            _logger.LogWarning("Preflight {ImportProcessId} was settled as {Status} before it finished.", run.Id, run.Status);
            return Result.Success();
        }

        if (passFailure is not null)
            return await FailRun(run, passFailure, cancellationToken);

        var (passed, rejected) = SettlePreflightRows(run, rehearsal, definition.Passes.Count);
        run.RecordProgress(passed, rejected, _dateTimeProvider.Now);

        if (stoppedEarly)
            return await CancelRun(run, cancellationToken);

        if (definition.Atomicity == ImportAtomicity.Atomic && rejected > 0)
        {
            // The count is left to the run's own totals, which every reader of this message already shows.
            run.Fail(
                "This import applies as one unit, so importing this file would apply none of it until every rejected row is fixed.",
                _dateTimeProvider.Now);
            await _importDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Preflight {ImportProcessId} finished: {Passed} passed, {Rejected} rejected; the atomic import would apply nothing.",
                run.Id, passed, rejected);

            return Result.Success();
        }

        return await CompleteRun(run, cancellationToken);
    }

    /// <summary>Advances the rehearsal copies. Settling them is left to <see cref="SettlePreflightRows"/>.</summary>
    private void RecordRehearsalOutcomes(IReadOnlyList<ImportProcessRow> rows, ImportPassResult result, int passIndex)
    {
        var byImportId = rows.ToDictionary(r => r.ImportId, StringComparer.Ordinal);
        var now = _dateTimeProvider.Now;

        foreach (var outcome in result.Rows)
        {
            if (!byImportId.TryGetValue(outcome.ImportId, out var row))
                continue;

            if (outcome.Failed)
            {
                row.MarkFailed(outcome.Error ?? "The row was rejected.", now);
                continue;
            }

            // A later pass may find the record by the id an earlier one created, inside the same transaction.
            if (outcome.CreatedEntityId is { } createdEntityId)
                row.RecordCreatedEntity(createdEntityId);

            if (outcome.Warning is not null)
                row.RecordWarning(outcome.Warning);

            row.RecordPassCompleted(passIndex);
        }
    }

    /// <summary>
    /// Copies each rehearsal's outcome onto the run's own row and returns how many passed and were rejected.
    /// A row that did not get through every pass without being rejected was never reached, and is left for
    /// the cancellation to mark.
    /// </summary>
    private (int Passed, int Rejected) SettlePreflightRows(ImportProcess run, List<ImportProcessRow> rehearsal, int passCount)
    {
        var byImportId = rehearsal.ToDictionary(r => r.ImportId, StringComparer.Ordinal);
        var now = _dateTimeProvider.Now;
        var passed = 0;
        var rejected = 0;

        foreach (var row in run.Rows)
        {
            if (!byImportId.TryGetValue(row.ImportId, out var outcome))
                continue;

            if (outcome.Status == ImportRowStatus.Failed)
            {
                row.MarkFailed(outcome.Error!, now);
                rejected++;
            }
            else if (outcome.CompletedPassCount == passCount)
            {
                row.MarkPassedPreflight(outcome.Warning, now);
                passed++;
            }
        }

        return (passed, rejected);
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
            process.IsPreflight
                ? $"An unexpected error stopped this preflight on each of {process.AttemptCount} attempts. Nothing was imported; check the file again once the cause is fixed.{reference}"
                : $"An unexpected error stopped this import on each of {process.AttemptCount} attempts. Rows it had already applied are unchanged, and the rest can be resumed.{reference}",
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

    /// <summary>How a pass's rows are split for one run: never, by count, or by count without splitting a group.</summary>
    /// <remarks>
    /// An atomic import is never split, whatever its passes declare: discarding staged work only holds while
    /// none of it has been saved, and a second chunk would mean the first already had. A per-group import is
    /// split only between groups, for the same reason at a smaller scale — each chunk is saved, so a group
    /// straddling two would be half applied the moment the first was.
    /// </remarks>
    private static List<IReadOnlyList<ImportProcessRow>> ChunksFor(
        IImportDefinition definition, ImportPassDescriptor pass, List<ImportProcessRow> eligible)
    {
        if (pass.Scope == ImportPassScope.WholeSet || definition.Atomicity == ImportAtomicity.Atomic)
            return [eligible];

        return definition.Atomicity == ImportAtomicity.PerGroup
            ? ChunkByGroup(eligible, definition.ChunkSize)
            : Chunk(eligible, definition.ChunkSize);
    }

    private static List<IReadOnlyList<ImportProcessRow>> Chunk(List<ImportProcessRow> rows, int chunkSize) =>
        [.. rows.Chunk(Math.Max(1, chunkSize)).Select(c => (IReadOnlyList<ImportProcessRow>)c)];

    /// <summary>
    /// Fills each chunk with whole groups up to the chunk size. A group larger than that is a chunk of its
    /// own rather than being split. Groups keep the order their first row had in the file.
    /// </summary>
    private static List<IReadOnlyList<ImportProcessRow>> ChunkByGroup(List<ImportProcessRow> rows, int chunkSize)
    {
        List<IReadOnlyList<ImportProcessRow>> chunks = [];
        List<ImportProcessRow> current = [];

        foreach (var group in rows.GroupBy(GroupOf, StringComparer.Ordinal))
        {
            var members = group.ToList();
            if (current.Count > 0 && current.Count + members.Count > chunkSize)
            {
                chunks.Add(current);
                current = [];
            }

            current.AddRange(members);
        }

        if (current.Count > 0)
            chunks.Add(current);

        return chunks;
    }

    /// <summary>A row's group. A row the submission gave none is its own group, which is what it would be anyway.</summary>
    private static string GroupOf(ImportProcessRow row) => row.GroupKey ?? row.ImportId;

    private async Task<Result<ImportPassResult>> ExecuteChunk(
        IImportDefinition definition,
        Guid importProcessId,
        int passIndex,
        IReadOnlyList<ImportProcessRow> rows,
        bool isFinalChunk,
        CancellationToken cancellationToken) =>
        definition.Atomicity == ImportAtomicity.PerGroup
            ? await ExecuteByGroup(definition, importProcessId, passIndex, rows, isFinalChunk, cancellationToken)
            : await definition.ExecutePass(importProcessId, passIndex, rows, isFinalChunk, cancellationToken);

    /// <summary>
    /// Runs a chunk of a per-group import, keeping out every group in which a row was rejected.
    /// </summary>
    /// <remarks>
    /// The pass is given the whole chunk, so its lookups stay batched and a clean chunk costs one call. When
    /// a row is rejected, everything the pass staged is thrown away and the chunk run again without the
    /// rejected groups, until a run rejects nothing. Discarding and re-running asks nothing of the definition
    /// beyond what every pass already does — read what it needs from the database each time it is called —
    /// where undoing one group's changes in place would need the runner to know which entities belong to it.
    /// Each re-run drops at least one group, so it ends; a clean chunk is never re-run.
    /// <para>
    /// A row kept out only for its group's sake says so, and names the row that was rejected, so the details
    /// page shows why a valid row did not apply.
    /// </para>
    /// </remarks>
    private async Task<Result<ImportPassResult>> ExecuteByGroup(
        IImportDefinition definition,
        Guid importProcessId,
        int passIndex,
        IReadOnlyList<ImportProcessRow> rows,
        bool isFinalChunk,
        CancellationToken cancellationToken)
    {
        var remaining = rows.ToList();
        List<ImportRowResult> keptOut = [];

        while (true)
        {
            var result = await definition.ExecutePass(importProcessId, passIndex, remaining, isFinalChunk, cancellationToken);
            if (result.IsFailure)
                return result;

            var outcomes = result.Value.Rows.ToDictionary(o => o.ImportId, StringComparer.Ordinal);

            // The first rejection in each group is the one its other rows are kept out on account of.
            var rejectedGroups = remaining
                .Where(r => outcomes.TryGetValue(r.ImportId, out var o) && o.Failed)
                .GroupBy(GroupOf, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().ImportId, StringComparer.Ordinal);

            if (rejectedGroups.Count == 0)
                return Result.Success(new ImportPassResult([.. result.Value.Rows, .. keptOut]));

            DiscardEverythingStaged();

            foreach (var row in remaining.Where(r => rejectedGroups.ContainsKey(GroupOf(r))))
            {
                keptOut.Add(outcomes.TryGetValue(row.ImportId, out var outcome) && outcome.Failed
                    ? outcome
                    : new ImportRowResult(
                        row.ImportId,
                        Failed: true,
                        $"Not applied: another row for the same {definition.GroupNoun} was rejected (import id '{rejectedGroups[GroupOf(row)]}').",
                        CreatedEntityId: null,
                        Warning: null));
            }

            remaining = [.. remaining.Where(r => !rejectedGroups.ContainsKey(GroupOf(r)))];
            if (remaining.Count == 0)
                return Result.Success(new ImportPassResult(keptOut));
        }
    }

    /// <summary>
    /// Lets go of every entity the attempt touched, however it touched it, so a re-run starts from what the
    /// database holds.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="DiscardStagedChanges"/>, which detaches only what was added, changed or removed:
    /// an aggregate the attempt loaded is still Unchanged while the entities it staged sit in its
    /// collections, and the next change detection would find them there and add them straight back.
    /// </remarks>
    private void DiscardEverythingStaged()
    {
        var touched = _importDbContext.ChangeTracker.Entries()
            .Where(e => e.Entity is not ImportProcess and not ImportProcessRow)
            .ToList();

        foreach (var entry in touched)
        {
            entry.State = EntityState.Detached;
        }
    }
}
