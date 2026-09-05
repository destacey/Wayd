using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Domain.Imports;

/// <summary>
/// One row of a submitted file, and what became of it.
/// </summary>
/// <remarks>
/// The runner claims rows by <see cref="Status"/> rather than reading the file again, which is what makes a
/// redelivered message, a resumed cancellation and a retry of the failures all the same operation over a
/// different filter — and what keeps an at-least-once redelivery from reapplying work.
/// </remarks>
public sealed class ImportProcessRow : BaseEntity
{
    private ImportProcessRow() { }

    private ImportProcessRow(string importId, int rowNumber, string payload)
    {
        ImportId = importId;
        RowNumber = rowNumber;
        Payload = payload;
        Status = ImportRowStatus.Pending;
    }

    public Guid ImportProcessId { get; private set; }

    /// <summary>
    /// The client's own correlation key for this row, unique within the file. Results are reported against
    /// it, and rows in the same file reference each other by it.
    /// </summary>
    public string ImportId { get; private set; } = default!;

    /// <summary>Position in the submitted file, so an error can name a line the user can find.</summary>
    public int RowNumber { get; private set; }

    /// <summary>
    /// The parsed row, as JSON. Held only while it is still needed: cleared the moment the row succeeds, and
    /// swept for whatever remains once the run passes its retention window. Kept as plain JSON rather than
    /// an encrypted column so an erasure request can reach it with a query.
    /// </summary>
    public string? Payload { get; private set; }

    public ImportRowStatus Status { get; private set; }

    /// <summary>Identifies the record this row created, so a result can be reported against the caller's key.</summary>
    public Guid? CreatedEntityId { get; private set; }

    public string? Error { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    public Instant? AttemptedOn { get; private set; }

    public static ImportProcessRow Create(string importId, int rowNumber, string payload) =>
        new(importId, rowNumber, payload);

    /// <summary>
    /// Drops the payload along with the status, because a succeeded row's payload has no remaining job — the
    /// record it created is the better record of it, and keeping a copy only widens the personal data held.
    /// </summary>
    public void MarkSucceeded(Guid? createdEntityId, Instant timestamp)
    {
        Status = ImportRowStatus.Succeeded;
        CreatedEntityId = createdEntityId;
        AttemptedOn = timestamp;
        Error = null;
        Payload = null;
    }

    /// <summary>Keeps the payload, which is what a retry re-executes.</summary>
    public void MarkFailed(string error, Instant timestamp)
    {
        Status = ImportRowStatus.Failed;
        Error = error;
        AttemptedOn = timestamp;
    }

    /// <summary>Marks a row the run never reached, so it is distinguishable from one that was tried and failed.</summary>
    public void MarkCancelled(Instant timestamp)
    {
        if (Status != ImportRowStatus.Pending)
            return;

        Status = ImportRowStatus.Cancelled;
        AttemptedOn = timestamp;
    }

    /// <summary>
    /// Returns a failed or cancelled row to the queue. Refuses a succeeded one, whose payload is gone and
    /// whose record already exists — reapplying it would duplicate — and refuses any row the retention
    /// sweep has already emptied, which can no longer be executed.
    /// </summary>
    public Result Reset()
    {
        if (Status == ImportRowStatus.Succeeded)
            return Result.Failure("A row that already succeeded cannot be attempted again.");

        if (Payload is null)
            return Result.Failure("This row's data has passed its retention window and is no longer available to retry.");

        Status = ImportRowStatus.Pending;
        Error = null;
        AttemptedOn = null;

        return Result.Success();
    }

    /// <summary>Retention sweep: drops the payload of a row the run is finished with.</summary>
    public void PurgePayload() => Payload = null;
}
