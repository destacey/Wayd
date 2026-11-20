using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Events.Kpis;

/// <summary>
/// A KPI's checkpoint plan was replaced: checkpoints added, removed or revised in one edit.
/// </summary>
/// <remarks>
/// The plan is saved whole, so one event covers every checkpoint the save added, removed or revised. Checkpoints are
/// matched by date: one on a day the plan already held, with a new label or values, is revised, not removed and added.
/// </remarks>
public sealed record KpiCheckpointPlanChangedEvent : DomainEvent<KpiCheckpointPlanChangedEvent>, IDomainEventDescriptor, IKpiEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public KpiCheckpointPlanChangedEvent(
        Guid id,
        int key,
        string subjectType,
        Guid subjectId,
        IReadOnlyList<KpiCheckpointValues> added,
        IReadOnlyList<KpiCheckpointValues> removed,
        IReadOnlyList<KpiCheckpointRevision> revised,
        IReadOnlyList<KpiCheckpointValues> plan,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Added = added;
        Removed = removed;
        Revised = revised;
        Plan = plan;

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

    /// <summary>The checkpoints on days the plan did not hold before, in date order.</summary>
    public IReadOnlyList<KpiCheckpointValues> Added { get; }

    /// <summary>The checkpoints on days the plan no longer holds, as they were, in date order.</summary>
    public IReadOnlyList<KpiCheckpointValues> Removed { get; }

    /// <summary>The checkpoints kept on their day with a new label or values, both ends, in date order.</summary>
    public IReadOnlyList<KpiCheckpointRevision> Revised { get; }

    /// <summary>The whole plan after the change, in date order; empty when it was cleared.</summary>
    public IReadOnlyList<KpiCheckpointValues> Plan { get; }

    [JsonIgnore]
    public string AggregateType => "Kpi";

    [JsonIgnore]
    public Guid AggregateId => Id;

    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => KpiEventRelations.For(SubjectType, SubjectId);
}

/// <summary>A checkpoint kept on its day with a new label or values: both ends of the change.</summary>
/// <param name="Previous">The checkpoint as it was.</param>
/// <param name="Current">The checkpoint as it is.</param>
public sealed record KpiCheckpointRevision(KpiCheckpointValues Previous, KpiCheckpointValues Current);
