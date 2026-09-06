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
    /// <summary>
    /// Storage bound for <see cref="ImportId"/>. Declared here so the submission that rejects an overlong
    /// key and the EF configuration that would otherwise fail on it read the same number.
    /// </summary>
    public const int MaxImportIdLength = 128;

    /// <summary>Storage bound for <see cref="Error"/> and <see cref="Warning"/>.</summary>
    public const int MaxMessageLength = 2048;

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
    /// <remarks>
    /// Belongs to the import, not to what the import creates. It is never written onto the record a row
    /// produces, and it does not survive as a way to find that record later: the durable link runs the
    /// other way, as <see cref="CreatedEntityId"/> recorded here and handed back in the results.
    /// <para>
    /// Deliberately a string rather than a number, because the useful case is the caller pasting the key
    /// their own system uses, which is often neither numeric nor free of leading zeros. A caller who only
    /// wants row numbers writes those instead — the string holds both.
    /// </para>
    /// <para>
    /// Uniqueness is case-insensitive, which is the collation of the index enforcing it rather than a
    /// choice made here.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Truncated to what the column holds rather than rejected: a pass can produce a long message, and a
    /// clipped explanation of why a row failed beats losing the whole save to it.
    /// </summary>
    public string? Error { get; private set => field = Clip(value); }

    /// <summary>
    /// Set when the row was applied but something about it was not. An employee whose manager number could
    /// not be resolved is imported without a manager rather than rejected, and that is worth telling the
    /// person who ran the import — it would otherwise only reach a log nobody reads.
    /// </summary>
    public string? Warning { get; private set => field = Clip(value); }

    public Instant? AttemptedOn { get; private set; }

    public static ImportProcessRow Create(string importId, int rowNumber, string payload) =>
        new(importId, rowNumber, payload);

    /// <summary>
    /// Notes the record this row created while the run is still working through its later passes. The row
    /// is not finished yet — a created employee still has to survive the manager and deactivation passes —
    /// so the id is held until <see cref="MarkSucceeded"/> carries it forward.
    /// </summary>
    public void RecordCreatedEntity(Guid createdEntityId) => CreatedEntityId = createdEntityId;

    /// <summary>Notes something the person should see about a row that still applied.</summary>
    public void RecordWarning(string warning) => Warning = warning;

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

    private static string? Clip(string? message)
    {
        var trimmed = message.NullIfWhiteSpacePlusTrim();

        return trimmed?.Length > MaxMessageLength
            ? trimmed[..MaxMessageLength]
            : trimmed;
    }
}
