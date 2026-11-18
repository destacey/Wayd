using Wayd.Common.Application.Imports;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// How one kind of record answers whether it exists, whether the caller may read its KPIs, and whether the caller
/// may change them.
/// </summary>
/// <remarks>
/// The records and the rules for who may see them belong to the modules, so each module implements this once per
/// subject type, in its own Application project, and Infrastructure registers it. Every KPI command and query asks
/// it first, through <see cref="KpiSubjectAccessResolver"/>, so no endpoint, import or MCP tool can disagree about
/// who may change a record's KPIs.
/// <para>
/// <strong>It decides from the record as it is now.</strong> Nothing about access is stored on a KPI, so a team
/// moved under another team of teams changes who may manage its KPIs the moment it moves.
/// </para>
/// <para>
/// <strong>Deliberately unmarked</strong>, like <c>IDiscussionChannelType</c>: the marker scan binds one
/// implementation per interface, so several subject types would collapse to one. Registration is explicit, and the
/// implementation must be <c>public</c>, because Wolverine's generated handlers construct it directly.
/// </para>
/// </remarks>
public interface IKpiSubjectType
{
    /// <summary>The registered subject type this answers for, spelled as its descriptor's key.</summary>
    string SubjectType { get; }

    /// <summary>
    /// What the caller may do with the record's KPIs, or null when they cannot read the record at all — or it does
    /// not exist — which every caller reports as not found.
    /// </summary>
    /// <remarks>
    /// Answer by the record's own rule only. The KPI administrator's waiver and a read-only token's limit are applied
    /// once, by <see cref="KpiSubjectAccessResolver"/>.
    /// </remarks>
    Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken);

    /// <summary>
    /// The link to each of the records that exists, keyed by id; an id with no record is left out.
    /// </summary>
    /// <remarks>
    /// Asks nothing of the caller, so an import pass running after its request has ended can tell which rows
    /// name a real record, and its results can link a KPI's row to the record it was added to. Whoever submitted
    /// the import has already been checked by the import's own permission.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken);
}

/// <summary>
/// One record as KPIs see it: what to call it, and what the caller may do with its KPIs.
/// </summary>
/// <param name="Name">The record's name, for a heading.</param>
/// <param name="Code">The record's short code or key, for display, when it has one.</param>
/// <param name="CanManage">Whether the caller may add, change and remove its KPIs and their measurements.</param>
public sealed record KpiSubjectAccess(string Name, string? Code, bool CanManage);
