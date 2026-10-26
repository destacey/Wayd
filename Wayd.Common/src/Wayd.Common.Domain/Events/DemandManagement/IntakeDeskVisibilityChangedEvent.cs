using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk's visibility changed, and with it who can read its requests from this moment on.
/// </summary>
public sealed record IntakeDeskVisibilityChangedEvent : DomainEvent<IntakeDeskVisibilityChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskVisibilityChangedEvent(Guid id, int key, IntakeDeskVisibility fromVisibility,
        IntakeDeskVisibility toVisibility, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromVisibility = fromVisibility;
        ToVisibility = toVisibility;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public IntakeDeskVisibility FromVisibility { get; }
    public IntakeDeskVisibility ToVisibility { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
