namespace Wayd.Common.Application.Imports;

/// <summary>
/// One row handed to a pass: its identity, its parsed data, and the outcome the pass records against it.
/// </summary>
/// <remarks>
/// A pass reports outcomes here rather than touching <c>ImportProcessRow</c>, so a pass stays a pure
/// function of its rows and can be tested without persistence. The runner reads the outcomes back and is
/// the only thing that writes row state.
/// </remarks>
public sealed class ImportRowItem<TRow>
{
    internal ImportRowItem(string importId, int rowNumber, TRow data)
    {
        ImportId = importId;
        RowNumber = rowNumber;
        Data = data;
    }

    /// <summary>The caller's key for this row. Results are reported against it, and rows reference each other by it.</summary>
    public string ImportId { get; }

    /// <summary>Position in the submitted file, so an error can name a line the user can find.</summary>
    public int RowNumber { get; }

    public TRow Data { get; }

    internal bool IsFailed { get; private set; }
    internal string? Warning { get; private set; }
    internal string? Error { get; private set; }
    internal Guid? CreatedEntityId { get; private set; }
    internal bool CreatedEntityIdSet { get; private set; }

    /// <summary>
    /// Records the record this row created. Only the pass that creates it calls this; later passes that
    /// merely touch the row leave the id alone.
    /// </summary>
    public void Created(Guid createdEntityId)
    {
        CreatedEntityId = createdEntityId;
        CreatedEntityIdSet = true;
    }

    /// <summary>
    /// Accepts the row but notes something about it — the row still counts as applied. Same rule as an
    /// error: name the field and the row, never the value.
    /// </summary>
    public void Warned(string warning) => Warning = warning;

    /// <summary>
    /// Rejects the row. Errors name the field and the row, never the value — a payload is personal data and
    /// an error message is not covered by the retention sweep.
    /// </summary>
    public void Failed(string error)
    {
        IsFailed = true;
        Error = error;
    }
}
