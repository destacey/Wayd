using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A measurement recorded against a KPI was changed.
/// </summary>
public sealed record KpiMeasurementChangedEvent : DomainEvent<KpiMeasurementChangedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiMeasurementChangedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiMeasurementValues measurement,
        KpiMeasurementValues previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Measurement = measurement;
        Previous = previous;

        Timestamp = timestamp;
    }

    /// <summary>The KPI's id.</summary>
    public Guid Id { get; }

    /// <summary>The KPI's key.</summary>
    public int Key { get; }

    /// <inheritdoc/>
    public string SubjectType { get; }

    /// <inheritdoc/>
    public Guid SubjectId { get; }

    /// <summary>The measurement after the change.</summary>
    public KpiMeasurementValues Measurement { get; }

    /// <summary>The measurement as it was before.</summary>
    public KpiMeasurementValues Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
