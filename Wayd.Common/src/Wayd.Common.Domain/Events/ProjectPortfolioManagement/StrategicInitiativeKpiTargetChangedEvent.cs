using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative KPI's starting value, target value or target direction changed.
/// </summary>
/// <remarks>
/// Carries both ends: a moved target is how an initiative's success gets redefined after the fact, and a
/// reader needs the goal it was held to before.
/// </remarks>
public sealed record StrategicInitiativeKpiTargetChangedEvent : DomainEvent<StrategicInitiativeKpiTargetChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiTargetChangedEvent(
        Guid id,
        int key,
        Guid kpiId,
        double? startingValue,
        double targetValue,
        KpiTargetDirection targetDirection,
        StrategicInitiativeKpiTarget previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        StartingValue = startingValue;
        TargetValue = targetValue;
        TargetDirection = targetDirection;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public double? StartingValue { get; }
    public double TargetValue { get; }
    public KpiTargetDirection TargetDirection { get; }

    /// <summary>The target this change replaced.</summary>
    public StrategicInitiativeKpiTarget Previous { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
