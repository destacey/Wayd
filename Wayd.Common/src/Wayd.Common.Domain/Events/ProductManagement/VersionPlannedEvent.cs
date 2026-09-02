using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A version was cut against a product node.
/// </summary>
public sealed record VersionPlannedEvent : DomainEvent, IProductManagementEvent
{
    public VersionPlannedEvent(
        Guid id,
        int key,
        Guid productId,
        string productName,
        string number,
        string? name,
        LocalDate? targetDate,
        Guid statusId,
        StatusCategory statusCategory,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ProductId = productId;
        ProductName = productName;
        Number = number;
        Name = name;
        TargetDate = targetDate;
        StatusId = statusId;
        StatusCategory = statusCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ProductId { get; }

    /// <summary>
    /// The product's name at the time, so a notification renders without a query and stays accurate
    /// after a rename.
    /// </summary>
    public string ProductName { get; }

    /// <summary>The version as entered. Free text, never parsed.</summary>
    public string Number { get; }

    public string? Name { get; }
    public LocalDate? TargetDate { get; }
    public Guid StatusId { get; }
    public StatusCategory StatusCategory { get; }
}
