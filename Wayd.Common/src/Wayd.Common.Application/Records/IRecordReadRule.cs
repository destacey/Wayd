namespace Wayd.Common.Application.Records;

/// <summary>
/// How one owner type answers "which of these records may the caller read" and "what is each one called".
/// </summary>
/// <remarks>
/// Anything in Common that points at a module's records by owner type and id — a follow today, a
/// notification next — must show a record only to someone who can read it, and must name it as it is now.
/// The records and the rule for who may read them belong to the modules, so each module implements this
/// once per owner type, in its own Application project, and Infrastructure registers it. Everything that
/// points at records shares that one rule, so no two features can disagree about who may see a record.
/// <para>
/// <strong>It only ever narrows.</strong> <see cref="ReadableIds"/> must apply exactly the rule the
/// record's own read endpoints apply — never a looser one, and never one that consults follows,
/// notifications or anything else pointing at the record. Being pointed at is not evidence of access.
/// </para>
/// <para>
/// <see cref="ReadableIds"/> must be a query the database can run, not a list built in memory: callers
/// join it to their own rows, then count and page in SQL. A rule that could only be evaluated in memory
/// would force loading every row of the type.
/// </para>
/// <para>
/// <strong>Deliberately unmarked</strong>, like <c>IStatusRecordMigrator</c>: the marker scan binds one
/// implementation per interface, so several read rules would collapse to one. Registration is explicit.
/// </para>
/// </remarks>
public interface IRecordReadRule
{
    /// <summary>The registered owner type this rule answers for.</summary>
    string OwnerType { get; }

    /// <summary>
    /// The ids of the records of this owner type the current caller may read, or <c>null</c> when they may
    /// read none — for example, when they lack the type's view permission.
    /// </summary>
    Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken);

    /// <summary>
    /// What to show for each of the given records. A record that no longer exists is absent.
    /// </summary>
    /// <remarks>
    /// Called only with ids that <see cref="ReadableIds"/> already admitted, and with no more than a page
    /// of them.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

/// <summary>
/// What a record is called, as its own owner type names it.
/// </summary>
/// <param name="Key">The record's human-readable key, as its own pages address it.</param>
/// <param name="Name">The record's name or title.</param>
/// <param name="ParentKey">
/// The key of the record this one is addressed under, for an owner type whose pages nest — a planning
/// interval objective under its planning interval. Null otherwise.
/// </param>
/// <param name="ParentName">The name of that parent record. Null when there is no parent.</param>
public sealed record RecordSummary(string Key, string Name, string? ParentKey = null, string? ParentName = null);
