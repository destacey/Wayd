using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI was added to a record's scorecard.
/// </summary>
/// <remarks>Carries the KPI as it was added, captured when it was created; only the key is read after the first save.</remarks>
public sealed record KpiAddedEvent : DomainEvent<KpiAddedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public KpiAddedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiDetails details,
        KpiGoal goal,
        KpiSource source,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Details = details;
        Goal = goal;
        Source = source;
        Order = order;

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

    /// <summary>What the KPI is called and how it is shown.</summary>
    public KpiDetails Details { get; }

    /// <summary>What the KPI is measured against.</summary>
    public KpiGoal Goal { get; }

    /// <summary>Where its measurements come from.</summary>
    public KpiSource Source { get; }

    /// <summary>Its position on the scorecard, lowest first.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
