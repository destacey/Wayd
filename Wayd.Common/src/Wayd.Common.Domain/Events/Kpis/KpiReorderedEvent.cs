using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI moved to another position on its record's scorecard.
/// </summary>
/// <remarks>Raised by each KPI whose position changed, so reordering the scorecard records one entry per KPI moved.</remarks>
public sealed record KpiReorderedEvent : DomainEvent<KpiReorderedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiReorderedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        int fromOrder,
        int toOrder,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        FromOrder = fromOrder;
        ToOrder = toOrder;

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

    /// <summary>The KPI's position before the move, lowest first.</summary>
    public int FromOrder { get; }

    /// <summary>The KPI's position after the move, lowest first.</summary>
    public int ToOrder { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
