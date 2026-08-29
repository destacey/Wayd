using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Interfaces.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A new node was added to the product taxonomy.
/// </summary>
public sealed record ProductAddedEvent : DomainEvent, IProductManagementEvent, ISimpleProduct
{
    public ProductAddedEvent(ISimpleProduct product, Guid productTypeId, Guid? parentId, Guid statusId, StatusCategory statusCategory, EventActor actor, Instant timestamp)
        : this(product.Id, product.Key, product.Name, product.Description, productTypeId, parentId, statusId, statusCategory, actor, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `product` parameter cannot be bound).
    [JsonConstructor]
    public ProductAddedEvent(Guid id, int key, string name, string? description, Guid productTypeId, Guid? parentId, Guid statusId, StatusCategory statusCategory, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ProductTypeId = productTypeId;
        ParentId = parentId;
        StatusId = statusId;
        StatusCategory = statusCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public Guid ProductTypeId { get; }
    public Guid? ParentId { get; }
    public Guid StatusId { get; }
    public StatusCategory StatusCategory { get; }
}
