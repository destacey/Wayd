using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI category was defined.
/// </summary>
public sealed record KpiCategoryCreatedEvent : DomainEvent<KpiCategoryCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public KpiCategoryCreatedEvent(
        Guid id,
        string name,
        string? description,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Order = order;

        Timestamp = timestamp;
    }

    /// <summary>The category's id.</summary>
    public Guid Id { get; }

    /// <summary>What the category is called.</summary>
    public string Name { get; }

    /// <summary>What kind of KPI belongs in it.</summary>
    public string? Description { get; }

    /// <summary>Its position in the list, lowest first.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "KpiCategory";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
