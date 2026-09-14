using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// Tracking began for a product that existed before <see cref="ProductAddedEvent"/> was recorded. Carries that
/// event's payload, describing the product as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
/// <remarks>
/// Deliberately not an <see cref="IProductManagementEvent"/>: consumers dispatch on that marker, and a baseline
/// is never delivered to one.
/// </remarks>
public sealed record ProductBaselinedEvent : BaselineEvent<ProductBaselinedEvent, ProductAddedEvent>
{
    public ProductBaselinedEvent(Guid id, int key, string name, string? description, Guid productTypeId, Guid? parentId, Guid statusId, StatusCategory statusCategory, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Product", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        ProductTypeId = productTypeId;
        ParentId = parentId;
        StatusId = statusId;
        StatusCategory = statusCategory;
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
