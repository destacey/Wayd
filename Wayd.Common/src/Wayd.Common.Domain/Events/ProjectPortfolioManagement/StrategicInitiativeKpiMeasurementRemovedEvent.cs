using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A measurement was removed from a strategic initiative KPI.
/// </summary>
/// <remarks>
/// Removing the latest measurement changes the KPI's actual value, so this is a fact in its own right. Carries
/// the value and date that were taken away, since the measurement is gone.
/// </remarks>
public sealed record StrategicInitiativeKpiMeasurementRemovedEvent : DomainEvent<StrategicInitiativeKpiMeasurementRemovedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiMeasurementRemovedEvent(
        Guid id,
        int key,
        Guid kpiId,
        Guid measurementId,
        double actualValue,
        Instant measurementDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        MeasurementId = measurementId;
        ActualValue = actualValue;
        MeasurementDate = measurementDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public Guid MeasurementId { get; }
    public double ActualValue { get; }
    public Instant MeasurementDate { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
