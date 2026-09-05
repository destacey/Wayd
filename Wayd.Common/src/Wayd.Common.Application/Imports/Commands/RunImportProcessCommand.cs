using System.Diagnostics;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
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

        for (var passIndex = 0; passIndex < definition.Passes.Count && !stoppedEarly; passIndex++)
        {
            var pass = definition.Passes[passIndex];
            var eligible = process.Rows.Where(r => r.Status == ImportRowStatus.Pending).ToList();

            if (eligible.Count == 0)
                break;

            List<IReadOnlyList<ImportProcessRow>> chunks = pass.Scope == ImportPassScope.WholeSet
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
                    return await FailAtomicRun(process, pass.Name, cancellationToken);

                // One SaveChanges per chunk covers the pass's entity changes and the row state together, so
                // a crash can never leave records created against rows still marked Pending — which a
                // redelivery would then apply a second time.
                process.RecordProgress(succeeded: 0, failed: failedInChunk, _dateTimeProvider.Now);
                await _importDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return stoppedEarly
            ? await CancelRun(process, cancellationToken)
            : await CompleteRun(process, cancellationToken);
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
    /// An atomic import applies as one unit, so a single rejection keeps the whole file out. The pass is
    /// required to validate before it mutates, which is why nothing needs undoing here.
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
