using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>A work item or generic link was added, updated, or removed on an incident.</summary>
public sealed record IncidentTimelineLinkRecordedEvent
    : DomainEvent<IncidentTimelineLinkRecordedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentTimelineLinkRecordedEvent(
        Guid id,
        int key,
        Guid entryId,
        IncidentTimelineLinkKind kind,
        IncidentTimelineLinkAction action,
        string label,
        string? fromLabel,
        string? toLabel,
        Instant recordedAt,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        EntryId = entryId;
        Kind = kind;
        Action = action;
        Label = label;
        FromLabel = fromLabel;
        ToLabel = toLabel;
        RecordedAt = recordedAt;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid EntryId { get; }
    public IncidentTimelineLinkKind Kind { get; }
    public IncidentTimelineLinkAction Action { get; }
    public string Label { get; }
    public string? FromLabel { get; }
    public string? ToLabel { get; }
    public Instant RecordedAt { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
