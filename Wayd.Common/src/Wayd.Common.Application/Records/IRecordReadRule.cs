using Wayd.Common.Domain.Enums;

namespace Wayd.Common.Application.Records;

/// <summary>
/// How one owner type answers "which of these records may the caller read", "which of them need a look",
/// and "what does each one look like now".
/// </summary>
/// <remarks>
/// Anything in Common that points at a module's records by owner type and id — a follow today, a
/// notification next — must show a record only to someone who can read it, and must describe it as it is
/// now. The records and the rule for who may read them belong to the modules, so each module implements
/// this once per owner type, in its own Application project, and Infrastructure registers it. Everything
/// that points at records shares that one rule, so no two features can disagree about who may see a record.
/// <para>
/// <strong>It only ever narrows.</strong> <see cref="Readable"/> must apply exactly the rule the record's
/// own read endpoints apply — never a looser one, and never one that consults follows, notifications or
/// anything else pointing at the record. Being pointed at is not evidence of access.
/// </para>
/// <para>
/// <see cref="Readable"/> must be a query the database can run, not a list built in memory: callers join
/// it to their own rows and combine the owner types with <c>UNION ALL</c>, then filter, count and page in
/// SQL. Its rows therefore carry only what a list filters on, as plain columns — no converted values,
/// whose store types would differ between owner types. What a record looks like comes from
/// <see cref="Describe"/>, for one page at a time.
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
    /// The aggregate type this record's activity is logged under, the same one its own Activity endpoint
    /// reads. Records of different modules can share an id (a replicated projection keeps its source's id),
    /// so the activity log is only ever read by id and type together.
    /// </summary>
    string ActivityAggregateType { get; }

    /// <summary>
    /// The records of this owner type the current caller may read, optionally narrowed to those matching a
    /// search, or <c>null</c> when they may read none — for example, when they lack the type's view permission.
    /// </summary>
    /// <param name="search">
    /// Text the record's name contains, or its key matches; trimmed and non-empty, or <c>null</c> for no
    /// search. Each owner type matches its own key form.
    /// </param>
    Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken);

    /// <summary>
    /// What each of the given records looks like now. A record that no longer exists is absent.
    /// </summary>
    /// <remarks>
    /// Called only with ids that <see cref="Readable"/> already admitted, and with no more than a page of
    /// them.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

/// <summary>
/// What a list filters on for one readable record, as plain columns every owner type can produce alike.
/// </summary>
/// <remarks>
/// A class with settable properties rather than a record, so a read rule can project into it inside a query.
/// Every owner type must set every property, in the same order, for the projections to combine.
/// </remarks>
public sealed class RecordFacts
{
    /// <summary>The record's id.</summary>
    public Guid Id { get; init; }

    /// <summary>Whether the record's current health is <see cref="HealthStatus.Unhealthy"/>. False for an owner type with no health.</summary>
    public bool IsUnhealthy { get; init; }

    /// <summary>Whether the record has finished, whatever the outcome: its lifecycle is Completed or Canceled.</summary>
    public bool IsClosed { get; init; }
}

/// <summary>
/// What a record looks like now, as its own owner type describes it.
/// </summary>
public sealed record RecordSummary
{
    /// <summary>The record's human-readable key, as its own pages address it.</summary>
    public required string Key { get; init; }

    /// <summary>The record's name or title.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The key of the record this one is addressed under, for an owner type whose pages nest — a planning
    /// interval objective under its planning interval. Null otherwise.
    /// </summary>
    public string? ParentKey { get; init; }

    /// <summary>The name of that parent record. Null when there is no parent.</summary>
    public string? ParentName { get; init; }

    /// <summary>The record's status as its own pages name it, such as "Active" or "In Progress".</summary>
    public required string StatusName { get; init; }

    /// <summary>Where the status sits in the record's lifecycle.</summary>
    public required LifecycleCategory Lifecycle { get; init; }

    /// <summary>The record's current health, or null when it has none — expired, never checked, or an owner type without health.</summary>
    public HealthStatus? Health { get; init; }

    /// <summary>How far the record has got, 0 to 100, for an owner type that tracks it. Null otherwise.</summary>
    public double? Progress { get; init; }

    /// <summary>
    /// Who leads the record, in the order its owner type ranks them: a project's or program's owners then
    /// managers, an objective's team. Empty when no one does.
    /// </summary>
    public IReadOnlyList<RecordPerson> People { get; init; } = [];

    /// <summary>When the record starts, or null when it has no start.</summary>
    public LocalDate? Start { get; init; }

    /// <summary>When the record ends or is due, or null when it has no end.</summary>
    public LocalDate? End { get; init; }
}

/// <summary>A person leading a record, or the team owning it, and the role they hold.</summary>
/// <param name="Role">The role as the record's own pages name it, such as "Project Manager" or "Team".</param>
/// <param name="Name">The person's or team's name.</param>
/// <param name="EmployeeId">The employee's id when the holder is a person; null for a team.</param>
public sealed record RecordPerson(string Role, string Name, Guid? EmployeeId);
