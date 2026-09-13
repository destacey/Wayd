using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A KPI was added to a strategic initiative.
/// </summary>
/// <remarks>
/// The KPI is part of the initiative's aggregate, so the entry belongs to the initiative and carries the KPI
/// as it was added.
/// </remarks>
public sealed record StrategicInitiativeKpiAddedEvent : DomainEvent<StrategicInitiativeKpiAddedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpiAddedEvent(
        Guid id,
        int key,
        Guid kpiId,
        string name,
        string? description,
        double? startingValue,
        double targetValue,
        string? prefix,
        string? suffix,
        KpiTargetDirection targetDirection,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        KpiId = kpiId;
        Name = name;
        Description = description;
        StartingValue = startingValue;
        TargetValue = targetValue;
        Prefix = prefix;
        Suffix = suffix;
        TargetDirection = targetDirection;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid KpiId { get; }
    public string Name { get; }
    public string? Description { get; }
    public double? StartingValue { get; }
    public double TargetValue { get; }
    public string? Prefix { get; }
    public string? Suffix { get; }
    public KpiTargetDirection TargetDirection { get; }
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
