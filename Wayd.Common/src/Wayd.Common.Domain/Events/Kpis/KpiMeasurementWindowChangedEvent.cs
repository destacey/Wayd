using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// How a KPI's measurements are windowed changed, before any were recorded.
/// </summary>
/// <remarks>Carries both ends, so the activity shows what the KPI was set up as before.</remarks>
public sealed record KpiMeasurementWindowChangedEvent : DomainEvent<KpiMeasurementWindowChangedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiMeasurementWindowChangedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiMeasurementWindow window,
        KpiMeasurementWindow previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Window = window;
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

    /// <summary>How its measurements are windowed after the change.</summary>
    public KpiMeasurementWindow Window { get; }

    /// <summary>How they were windowed before.</summary>
    public KpiMeasurementWindow Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
