using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI category's name or description changed.
/// </summary>
public sealed record KpiCategoryDetailsUpdatedEvent : DomainEvent<KpiCategoryDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiCategoryDetailsUpdatedEvent(
        Guid id,
        string name,
        string? description,
        string previousName,
        string? previousDescription,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    /// <summary>The category's id.</summary>
    public Guid Id { get; }

    /// <summary>The name after the edit.</summary>
    public string Name { get; }

    /// <summary>The description after the edit.</summary>
    public string? Description { get; }

    /// <summary>The name the edit replaced.</summary>
    public string PreviousName { get; }

    /// <summary>The description the edit replaced.</summary>
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "KpiCategory";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
