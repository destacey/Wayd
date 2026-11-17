using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A closed KPI was reopened and is back on the scorecard.
/// </summary>
public sealed record KpiReopenedEvent : DomainEvent<KpiReopenedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public KpiReopenedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;

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

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
