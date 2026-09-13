using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A measurement was recorded against a strategic initiative KPI.
/// </summary>
/// <remarks>
/// A ledger entry, so it has one end. The person who took the measurement is carried by id.
/// </remarks>
public sealed record StrategicInitiativeKpiMeasurementAddedEvent : DomainEvent<StrategicInitiativeKpiMeasurementAddedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiMeasurementAddedEvent(
        Guid id,
        int key,
        Guid kpiId,
        Guid measurementId,
        double actualValue,
        Instant measurementDate,
        Guid measuredById,
        string? note,
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
        MeasuredById = measuredById;
        Note = note;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public Guid MeasurementId { get; }
    public double ActualValue { get; }
    public Instant MeasurementDate { get; }

    /// <summary>The employee who took the measurement.</summary>
    public Guid MeasuredById { get; }

    public string? Note { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
