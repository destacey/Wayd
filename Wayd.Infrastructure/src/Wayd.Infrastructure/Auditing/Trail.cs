using NodaTime;

namespace Wayd.Infrastructure.Auditing;

/// <summary>
/// One recorded change, as written to <c>Auditing.AuditTrails</c>.
/// </summary>
/// <remarks>
/// Deliberately not a <c>BaseEntity</c>. A <c>BaseEntity</c> is keyed on a <c>Guid</c> it assigns itself,
/// and it can raise domain events and carry post-persistence actions — which an audit row must never do,
/// being the record of a change rather than a participant in one.
/// <para>
/// The key is an identity instead. Nothing reads it, nothing references it and it is never exposed, so it
/// carries no meaning beyond ordering the rows — and this is an append-only table and the largest write
/// target in the application, where the order keys arrive in is the difference between appending to the
/// clustered index and splitting a page in the middle of it. An identity is monotonic across processes and
/// restarts, which no client-generated Guid is, and at eight bytes it also shrinks every nonclustered index
/// on the table, since each one carries the clustering key.
/// </para>
/// </remarks>
public sealed class Trail
{
    /// <summary>
    /// Assigned by the database on insert — see the remarks on the class.
    /// </summary>
    public long Id { get; private set; }

    public required string UserId { get; set; }
    public string? Type { get; set; }
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public Instant DateTime { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
    public string? PrimaryKey { get; set; }
    public string? CorrelationId { get; set; }
}
