using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI category stopped being offered for new KPIs; KPIs that have it keep it.
/// </summary>
public sealed record KpiCategoryDeactivatedEvent : DomainEvent<KpiCategoryDeactivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public KpiCategoryDeactivatedEvent(
        Guid id,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    /// <summary>The category's id.</summary>
    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "KpiCategory";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
