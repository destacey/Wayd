using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk's name or its requester-facing description changed.
/// </summary>
public sealed record IntakeDeskDetailsUpdatedEvent : DomainEvent<IntakeDeskDetailsUpdatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskDetailsUpdatedEvent(Guid id, int key, string name, string description, string previousName,
        string previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    /// <summary>The requester-facing description of what to bring to the desk.</summary>
    public string Description { get; }
    public string PreviousName { get; }
    public string PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
