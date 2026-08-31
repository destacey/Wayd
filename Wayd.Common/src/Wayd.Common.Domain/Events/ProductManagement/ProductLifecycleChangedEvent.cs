using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node moved to a different lifecycle status.
/// </summary>
/// <remarks>
/// <para>
/// The design called for separate <c>ProductSunset</c> and <c>ProductRetired</c> events. Those names
/// presume fixed statuses; with a configurable workflow an organization can add its own, and a fixed
/// pair of event types could not carry them — a node moving to a status somebody invented would raise
/// nothing at all, which is worse than a shared type.
/// </para>
/// <para>
/// The split survives where it matters, on the reading side rather than the type side:
/// <see cref="ToAlias"/> says whether this was a sunset, a retirement or something the organization
/// named itself, and <see cref="ToCategory"/> answers the coarse question without knowing the
/// workflow. A consumer that reacts differently to retirement still can; one that does not, does not
/// have to learn every status an organization invents.
/// </para>
/// </remarks>
public sealed record ProductLifecycleChangedEvent : DomainEvent, IProductManagementEvent
{
    public ProductLifecycleChangedEvent(
        Guid id,
        int key,
        string name,
        Guid fromStatusId,
        StatusCategory fromCategory,
        ProductStatusAlias fromAlias,
        Guid toStatusId,
        StatusCategory toCategory,
        ProductStatusAlias toAlias,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        FromStatusId = fromStatusId;
        FromCategory = fromCategory;
        FromAlias = fromAlias;
        ToStatusId = toStatusId;
        ToCategory = toCategory;
        ToAlias = toAlias;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }
    public ProductStatusAlias FromAlias { get; }

    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }

    /// <summary>
    /// The well-known meaning of the status the product moved to, where it has one. This is what a
    /// consumer branches on — never the status name, which an administrator may rename at any time.
    /// </summary>
    public ProductStatusAlias ToAlias { get; }
}
