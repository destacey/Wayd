using NodaTime;
using Wayd.Common.Domain.Interfaces.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A product node's name, description or external identifier changed.
/// </summary>
/// <remarks>
/// One type for all three deliberately. Renaming a product and rewording its description lead to
/// identical handling everywhere, so splitting them would produce ceremony rather than clarity — the
/// test for splitting is whether any consumer would react differently, not whether the fields differ.
/// <para>
/// Scoped rather than generic, which is what separates it from the <c>ProductUpdated</c> the design
/// warns against: it names which facet changed and carries the new values, instead of telling a
/// subscriber only to re-read the record. Changes a consumer <em>does</em> react differently to —
/// reparenting, retyping, a lifecycle move — have their own types.
/// </para>
/// </remarks>
public sealed record ProductDetailsUpdatedEvent : DomainEvent, IProductManagementEvent, ISimpleProduct
{
    public ProductDetailsUpdatedEvent(Guid id, int key, string name, string? description, string? externalId, EventActor actor, Instant timestamp)
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
