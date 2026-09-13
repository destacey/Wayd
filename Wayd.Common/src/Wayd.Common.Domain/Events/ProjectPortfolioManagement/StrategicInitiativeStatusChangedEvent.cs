using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative moved to a different status.
/// </summary>
/// <remarks>
/// One event for approval, activation, completion and cancellation, carrying the status as a name plus a
/// <see cref="LifecycleCategory"/>, for the reasons given on <see cref="ProgramStatusChangedEvent"/>.
/// </remarks>
public sealed record StrategicInitiativeStatusChangedEvent : DomainEvent<StrategicInitiativeStatusChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public StrategicInitiativeStatusChangedEvent(
        Guid id,
        int key,
        string fromStatus,
        LifecycleCategory fromCategory,
        string toStatus,
        LifecycleCategory toCategory,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatus = fromStatus;
        FromCategory = fromCategory;
        ToStatus = toStatus;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string FromStatus { get; }
    public LifecycleCategory FromCategory { get; }
    public string ToStatus { get; }
    public LifecycleCategory ToCategory { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
