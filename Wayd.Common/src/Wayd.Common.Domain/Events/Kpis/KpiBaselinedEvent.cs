using System.Text.Json.Serialization;
using Wayd.Common.Domain.Kpis;
using NodaTime;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// Tracking began for a KPI that existed before <see cref="KpiAddedEvent"/> was recorded for it, such as a strategic
/// initiative KPI moved onto the shared model. Carries that event's payload, describing the KPI as it stood at
/// <see cref="DomainEvent.Timestamp"/>. Also listed on its subject's Activity, as the creation event is.
/// </summary>
/// <remarks>
/// The KPI's measurements and checkpoint plan are not part of the payload, as they are not part of
/// <see cref="KpiAddedEvent"/>: they are on the KPI's own rows, and how they got there is the history that preceded
/// the baseline.
/// </remarks>
public sealed record KpiBaselinedEvent : BaselineEvent<KpiBaselinedEvent, KpiAddedEvent>, IRelatedAggregateEvent
{
    public KpiBaselinedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        KpiDetails details,
        KpiGoal goal,
        KpiSource source,
        int order,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp,
        KpiMeasurementWindow? window = null)
        : base("Kpi", id, recordCreatedOn, recordCreatedById, timestamp, "1.1")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Details = details;
        Goal = goal;
        Source = source;
        Order = order;
        Window = window ?? KpiMeasurementWindow.PerPeriod;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string SubjectType { get; }
    public Guid SubjectId { get; }
    public KpiDetails Details { get; }
    public KpiGoal Goal { get; }
    public KpiSource Source { get; }
    public int Order { get; }
    public KpiMeasurementWindow Window { get; }

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}
