using NodaTime;
using Wayd.Common.Domain.Interfaces.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node was pointed at the record that owns it in another system, or unlinked.
/// </summary>
/// <remarks>
/// Separate from a details change because a consumer does react differently: the link is what lets an
/// integration correlate a repository, pipeline or registry package back to a product, so one arriving
/// or disappearing changes what can be resolved — where a rename changes only what is displayed.
/// <para>
/// <see cref="ExternalId"/> is <c>null</c> when the link was cleared.
/// </para>
/// </remarks>
public sealed record ProductLinkedExternallyEvent : DomainEvent, IProductManagementEvent, ISimpleProduct
{
    public ProductLinkedExternallyEvent(Guid id, int key, string name, string? description, string? externalId, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ExternalId = externalId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public string? ExternalId { get; }
}
