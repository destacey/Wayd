using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>A commander added an entry to an incident's operational timeline.</summary>
public sealed record IncidentTimelineNoteAddedEvent : DomainEvent<IncidentTimelineNoteAddedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentTimelineNoteAddedEvent(
        Guid id,
        int key,
        Guid noteId,
        Guid authorEmployeeId,
        Instant recordedAt,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        NoteId = noteId;
        AuthorEmployeeId = authorEmployeeId;
        RecordedAt = recordedAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid NoteId { get; }
    public Guid AuthorEmployeeId { get; }
    public Instant RecordedAt { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
