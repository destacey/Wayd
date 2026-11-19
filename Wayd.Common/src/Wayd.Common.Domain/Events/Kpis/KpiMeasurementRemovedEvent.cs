using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A measurement was removed from a KPI.
/// </summary>
public sealed record KpiMeasurementRemovedEvent : DomainEvent<KpiMeasurementRemovedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public KpiMeasurementRemovedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiMeasurementValues measurement,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Measurement = measurement;

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

    /// <summary>The measurement as it was when removed.</summary>
    public KpiMeasurementValues Measurement { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
