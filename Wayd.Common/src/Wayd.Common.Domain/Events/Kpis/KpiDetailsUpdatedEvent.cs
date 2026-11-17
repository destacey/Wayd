using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's name, description, category, prefix, suffix or reading changed.
/// </summary>
public sealed record KpiDetailsUpdatedEvent : DomainEvent<KpiDetailsUpdatedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiDetailsUpdatedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiDetails details,
        KpiDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Details = details;
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

    /// <summary>The details after the edit.</summary>
    public KpiDetails Details { get; }

    /// <summary>The details the edit replaced.</summary>
    public KpiDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
