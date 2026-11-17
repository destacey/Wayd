using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI category no KPI used was deleted.
/// </summary>
public sealed record KpiCategoryDeletedEvent : DomainEvent<KpiCategoryDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public KpiCategoryDeletedEvent(
        Guid id,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The category's id.</summary>
    public Guid Id { get; }

    /// <summary>The category's name when it was deleted, since the record is gone.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "KpiCategory";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
