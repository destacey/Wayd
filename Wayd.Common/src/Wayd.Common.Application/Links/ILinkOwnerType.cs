using Wayd.Common.Domain.Events.Links;

namespace Wayd.Common.Application.Links;

/// <summary>
/// How one kind of record answers whether it exists, whether the caller may read its links, and whether the caller
/// may change them.
/// </summary>
/// <remarks>
/// The records and the rules for who may see and edit them belong to the modules, so each module implements this once
/// per owner type, in its own Application project, and Infrastructure registers it. Every link command and query asks
/// it first, so no endpoint can disagree about who may change a record's links.
/// <para>
/// <strong>It decides from the record as it is now.</strong> Nothing about access is stored on a link, so a project
/// moved to another portfolio changes who may manage its links the moment it moves.
/// </para>
/// <para>
/// <strong>Managing a record's links is editing the record.</strong> <see cref="Resolve"/> answers by the rule the
/// record's own edit applies — the same permission, the same role on the record, the same state that makes it
/// read-only — and never a looser one.
/// </para>
/// <para>
/// <strong>Deliberately unmarked</strong>, like <c>IKpiSubjectType</c>: the marker scan binds one implementation per
/// interface, so several owner types would collapse to one. Registration is explicit, and the implementation must be
/// <c>public</c>, because Wolverine's generated handlers construct it directly.
/// </para>
/// </remarks>
public interface ILinkOwnerType
{
    /// <summary>The registered owner type this answers for, spelled as its descriptor's key.</summary>
    string OwnerType { get; }

    /// <summary>
    /// What the caller may do with the record's links, or null when they cannot read the record at all — or it does
    /// not exist — which every caller reports as not found.
    /// </summary>
    /// <remarks>
    /// Answer by the record's own rule only. A read-only token's limit is applied once, for every owner type, by
    /// whoever asks.
    /// </remarks>
    Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies what a link change means for the record — an entry on an incident's timeline, say — to the module's
    /// own aggregate, tracked in the same unit of work, before the caller saves.
    /// </summary>
    /// <remarks>
    /// In the same save rather than a handler of the link's event, so the record and its links cannot disagree. It
    /// asks nothing of the caller: a change made by an import or by the record's own deletion reaches it too. Most
    /// records need nothing here.
    /// </remarks>
    Task OnChanged(LinkChange change, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>What the caller may do with one record's links, as its owner type answers it now.</summary>
/// <param name="CanManage">Whether the caller may add, change and remove its links.</param>
public sealed record LinkOwnerAccess(bool CanManage);

/// <summary>What happened to a link.</summary>
public enum LinkChangeKind
{
    /// <summary>The link was added to the record.</summary>
    Added,

    /// <summary>The link's name or address changed.</summary>
    Updated,

    /// <summary>The link was removed from the record.</summary>
    Removed,
}

/// <summary>
/// One change to a record's links, as its owner type is told of it.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="LinkId">The link's id.</param>
/// <param name="OwnerId">The record the link is on.</param>
/// <param name="Details">The link's name and address after the change, or as it was removed.</param>
/// <param name="Previous">The name and address an update replaced; null for any other change.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Timestamp">When it was made.</param>
public sealed record LinkChange(
    LinkChangeKind Kind,
    Guid LinkId,
    Guid OwnerId,
    LinkDetails Details,
    LinkDetails? Previous,
    EventActor Actor,
    Instant Timestamp);
