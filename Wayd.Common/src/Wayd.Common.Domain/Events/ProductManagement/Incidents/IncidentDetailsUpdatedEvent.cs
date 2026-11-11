using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>An incident's title, description, or severity changed.</summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="IncidentDetailsUpdatedEventV2"/> replaced it
/// when description split into symptoms and root cause. Kept so every payload written as this type still
/// deserializes into it — its name and members are the contract those payloads were written against.
/// </remarks>
[Obsolete("Superseded by IncidentDetailsUpdatedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record IncidentDetailsUpdatedEvent : DomainEvent<IncidentDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentDetailsUpdatedEvent(
        Guid id,
        int key,
        string previousTitle,
        string? previousDescription,
        Guid previousSeverityLevelId,
        string title,
        string? description,
        Guid severityLevelId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousTitle = previousTitle;
        PreviousDescription = previousDescription;
        PreviousSeverityLevelId = previousSeverityLevelId;
        Title = title;
        Description = description;
        SeverityLevelId = severityLevelId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string PreviousTitle { get; }
    public string? PreviousDescription { get; }
    public Guid PreviousSeverityLevelId { get; }
    public string Title { get; }
    public string? Description { get; }
    public Guid SeverityLevelId { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
