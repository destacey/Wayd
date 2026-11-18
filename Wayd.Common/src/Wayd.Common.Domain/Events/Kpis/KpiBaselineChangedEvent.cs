using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's baseline value or window changed.
/// </summary>
/// <remarks>Its own event rather than part of a target change: a corrected baseline moves every relative target and every progress reading, so it must be findable on its own.</remarks>
public sealed record KpiBaselineChangedEvent : DomainEvent<KpiBaselineChangedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiBaselineChangedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiBaseline baseline,
        KpiBaseline previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Baseline = baseline;
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

    /// <summary>The baseline after the change.</summary>
    public KpiBaseline Baseline { get; }

    /// <summary>The baseline it replaced.</summary>
    public KpiBaseline Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
