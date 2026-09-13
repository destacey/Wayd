using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative KPI's checkpoint plan was changed.
/// </summary>
/// <remarks>
/// The plan is saved whole, so one event covers every checkpoint the save added, removed or revised.
/// <see cref="Checkpoints"/> is the plan afterwards, for a consumer keeping a copy.
/// </remarks>
public sealed record StrategicInitiativeKpiCheckpointPlanChangedEvent : DomainEvent<StrategicInitiativeKpiCheckpointPlanChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiCheckpointPlanChangedEvent(
        Guid id,
        int key,
        Guid kpiId,
        StrategicInitiativeKpiCheckpointValues[] added,
        StrategicInitiativeKpiCheckpointValues[] removed,
        StrategicInitiativeKpiCheckpointRevision[] revised,
        StrategicInitiativeKpiCheckpointValues[] checkpoints,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        Added = [.. added];
        Removed = [.. removed];
        Revised = [.. revised];
        Checkpoints = [.. checkpoints];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public StrategicInitiativeKpiCheckpointValues[] Added { get; }
    public StrategicInitiativeKpiCheckpointValues[] Removed { get; }
    public StrategicInitiativeKpiCheckpointRevision[] Revised { get; }

    /// <summary>Every checkpoint in the plan after the change, in date order.</summary>
    public StrategicInitiativeKpiCheckpointValues[] Checkpoints { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
