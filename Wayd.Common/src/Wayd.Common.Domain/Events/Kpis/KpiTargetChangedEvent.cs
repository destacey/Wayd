using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's direction, target or at-risk threshold changed.
/// </summary>
/// <remarks>Carries both ends: a moved target is how success gets redefined after the fact, and a reader needs the goal the KPI was held to before.</remarks>
public sealed record KpiTargetChangedEvent : DomainEvent<KpiTargetChangedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiTargetChangedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiGoal goal,
        KpiGoal previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Goal = goal;
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

    /// <summary>What the KPI is measured against after the change.</summary>
    public KpiGoal Goal { get; }

    /// <summary>What it was measured against before.</summary>
    public KpiGoal Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
