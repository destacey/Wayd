using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A KPI was removed from a strategic initiative, along with its checkpoints and measurements.
/// </summary>
/// <remarks>
/// The name is carried because the KPI is gone by the time anyone reads the entry.
/// </remarks>
public sealed record StrategicInitiativeKpiRemovedEvent : DomainEvent<StrategicInitiativeKpiRemovedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiRemovedEvent(
        Guid id,
        int key,
        Guid kpiId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
