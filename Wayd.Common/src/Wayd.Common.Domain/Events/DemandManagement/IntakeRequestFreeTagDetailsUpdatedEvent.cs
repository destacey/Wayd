using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A free intake request tag was renamed or its description changed. Every record carrying it reads the new name.
/// </summary>
public sealed record IntakeRequestFreeTagDetailsUpdatedEvent : DomainEvent<IntakeRequestFreeTagDetailsUpdatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeRequestFreeTagDetailsUpdatedEvent(Guid id, string name, string? description, string previousName, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    /// <summary>The free tag.</summary>
    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public string PreviousName { get; }
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTag";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
