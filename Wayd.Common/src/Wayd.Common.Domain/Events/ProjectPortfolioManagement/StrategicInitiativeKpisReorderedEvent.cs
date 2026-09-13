using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative's KPIs were put in a different order.
/// </summary>
public sealed record StrategicInitiativeKpisReorderedEvent : DomainEvent<StrategicInitiativeKpisReorderedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeKpisReorderedEvent(
        Guid id,
        int key,
        Guid[] previousOrder,
        Guid[] order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousOrder = [.. previousOrder];
        Order = [.. order];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The KPI ids in their order before the change.</summary>
    public Guid[] PreviousOrder { get; }

    /// <summary>The KPI ids in their order after the change.</summary>
    public Guid[] Order { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
