using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Domain.Imports;

/// <summary>
/// One submitted import file: its lifecycle, its counts, and the rows it is applying.
/// </summary>
/// <remarks>
/// Deliberately not <c>ISystemAuditable</c>. The runner writes <see cref="RecordProgress"/> at every chunk
/// boundary, so auditing this would add an audit row per chunk and bury real entity changes.
/// </remarks>
public sealed class ImportProcess : BaseEntity
{
    private readonly List<ImportProcessRow> _rows = [];

    private ImportProcess() { }

    private ImportProcess(
        string importType,
        string submittedByUserId,
        Guid? submissionGroupId,
        IEnumerable<ImportProcessRow> rows,
        Instant timestamp)
    {
        ImportType = importType;
        SubmittedByUserId = submittedByUserId;
        SubmissionGroupId = submissionGroupId;
        SubmittedOn = timestamp;
        Status = ImportProcessStatus.Queued;
        _rows.AddRange(rows);
        TotalRowCount = _rows.Count;
    }

    /// <summary>Registry key of the import definition that governs this run.</summary>
    public string ImportType { get; private set; } = default!;

    public ImportProcessStatus Status { get; private set; }

    /// <summary>Groups the runs submitted together, so a seed of fifteen files reads as one entry.</summary>
    public Guid? SubmissionGroupId { get; private set; }

    /// <summary>
    /// Trace id of the most recent attempt, so the audit trail that attempt wrote can be found from here.
    /// Overwritten on each <see cref="Start"/>: a retry is a different execution under a different trace,
    /// and nothing else persists the id once the run ends.
    /// </summary>
    public string? LastAttemptCorrelationId { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>Matches the string user id the rest of the application uses, including AuditTrail.</summary>
    public string SubmittedByUserId { get; private set; } = default!;
    public Instant SubmittedOn { get; private set; }
    public Instant? StartedOn { get; private set; }
    public Instant? CompletedOn { get; private set; }

    /// <summary>
    /// Last time the runner made progress. Stall detection measures inactivity from here rather than from
    /// <see cref="StartedOn"/>, so a legitimately long import is never mistaken for a dead one.
    /// </summary>
    public Instant? LastProgressOn { get; private set; }

    public int TotalRowCount { get; private set; }
    public int SucceededRowCount { get; private set; }
    public int FailedRowCount { get; private set; }

    /// <summary>Why the run itself could not complete. Per-row reasons live on the rows.</summary>
    public string? Error { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    public IReadOnlyCollection<ImportProcessRow> Rows => _rows.AsReadOnly();

    public bool IsTerminal => Status
        is ImportProcessStatus.Succeeded
        or ImportProcessStatus.PartiallySucceeded
        or ImportProcessStatus.Failed
        or ImportProcessStatus.Cancelled;

    public static ImportProcess Create(
        string importType,
        string submittedByUserId,
        Guid? submissionGroupId,
        IEnumerable<ImportProcessRow> rows,
        Instant timestamp) =>
        new(importType, submittedByUserId, submissionGroupId, rows, timestamp);

    /// <summary>
    /// Claims the run for a worker. Rejecting anything but <see cref="ImportProcessStatus.Queued"/> is what
    /// makes an at-least-once redelivery safe: a second delivery of the same message finds it already
    /// claimed and stops instead of reapplying rows.
    /// </summary>
    public Result Start(string attemptCorrelationId, Instant timestamp)
    {
        if (Status != ImportProcessStatus.Queued)
            return Result.Failure($"An import can only start from Queued, but this one is {Status}.");

        Status = ImportProcessStatus.Processing;
        StartedOn = timestamp;
        LastProgressOn = timestamp;
        LastAttemptCorrelationId = attemptCorrelationId;

        return Result.Success();
    }

    /// <summary>Advances the counts and the heartbeat together, so progress is never recorded without one.</summary>
    public void RecordProgress(int succeeded, int failed, Instant timestamp)
    {
        SucceededRowCount += succeeded;
        FailedRowCount += failed;
        LastProgressOn = timestamp;
    }

    public Result RequestCancellation(Instant timestamp)
    {
        if (IsTerminal)
            return Result.Failure($"This import is already {Status}.");

        if (Status == ImportProcessStatus.Cancelling)
            return Result.Success();

        Status = ImportProcessStatus.Cancelling;
        LastProgressOn = timestamp;

        return Result.Success();
    }

    /// <summary>Derives the terminal status from the counts, so a caller cannot record one that disagrees.</summary>
    public Result Complete(Instant timestamp)
    {
        if (Status is not (ImportProcessStatus.Processing or ImportProcessStatus.Cancelling))
            return Result.Failure($"An import can only complete while running, but this one is {Status}.");

        Status = FailedRowCount switch
        {
            0 => ImportProcessStatus.Succeeded,
            _ when SucceededRowCount > 0 => ImportProcessStatus.PartiallySucceeded,
            _ => ImportProcessStatus.Failed,
        };

        CompletedOn = timestamp;
        LastProgressOn = timestamp;

        return Result.Success();
    }

    public Result Cancel(Instant timestamp)
    {
        if (Status is not (ImportProcessStatus.Cancelling or ImportProcessStatus.Queued or ImportProcessStatus.Processing))
            return Result.Failure($"This import is already {Status}.");

        Status = ImportProcessStatus.Cancelled;
        CompletedOn = timestamp;
        LastProgressOn = timestamp;

        return Result.Success();
    }

    /// <summary>Ends the run without applying more rows — a hard failure, or a stalled run the sweep reclaims.</summary>
    public Result Fail(string error, Instant timestamp)
    {
        if (IsTerminal)
            return Result.Failure($"This import is already {Status}.");

        Status = ImportProcessStatus.Failed;
        Error = error;
        CompletedOn = timestamp;
        LastProgressOn = timestamp;

        return Result.Success();
    }

    /// <summary>
    /// Returns a terminal run to the queue so its unapplied rows can be attempted again — the recovery path
    /// after a cancellation or a stalled worker. Rows already applied are untouched.
    /// </summary>
    public Result Requeue(Instant timestamp)
    {
        if (!IsTerminal)
            return Result.Failure($"Only a finished import can be requeued, but this one is {Status}.");

        Status = ImportProcessStatus.Queued;
        CompletedOn = null;
        Error = null;
        LastProgressOn = timestamp;

        return Result.Success();
    }
}
