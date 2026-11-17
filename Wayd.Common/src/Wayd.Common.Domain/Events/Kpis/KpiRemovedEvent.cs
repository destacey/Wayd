using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI was removed from its record, with its measurements.
/// </summary>
public sealed record KpiRemovedEvent : DomainEvent<KpiRemovedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public KpiRemovedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Name = name;

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

    /// <summary>The KPI's name when it was removed, since the record is gone.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
