using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>Wayd created the one Teams chat stored on an incident.</summary>
/// <remarks>
/// The previous end is empty: this records the first chat, and a later paste or removal is a different event.
/// The title is the chat's own name, not a person. People added only for the Graph call are not on this event.
/// </remarks>
public sealed record IncidentTeamsChatCreatedEvent
    : DomainEvent<IncidentTeamsChatCreatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentTeamsChatCreatedEvent(
        Guid id,
        int key,
        string? fromTeamsChatId,
        string? fromTeamsChatTitle,
        string? fromTeamsChatLink,
        bool fromTeamsChatCreatedByWayd,
        string? toTeamsChatId,
        string? toTeamsChatTitle,
        string? toTeamsChatLink,
        bool toTeamsChatCreatedByWayd,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromTeamsChatId = fromTeamsChatId;
        FromTeamsChatTitle = fromTeamsChatTitle;
        FromTeamsChatLink = fromTeamsChatLink;
        FromTeamsChatCreatedByWayd = fromTeamsChatCreatedByWayd;
        ToTeamsChatId = toTeamsChatId;
        ToTeamsChatTitle = toTeamsChatTitle;
        ToTeamsChatLink = toTeamsChatLink;
        ToTeamsChatCreatedByWayd = toTeamsChatCreatedByWayd;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string? FromTeamsChatId { get; }
    public string? FromTeamsChatTitle { get; }
    public string? FromTeamsChatLink { get; }
    public bool FromTeamsChatCreatedByWayd { get; }
    public string? ToTeamsChatId { get; }
    public string? ToTeamsChatTitle { get; }
    public string? ToTeamsChatLink { get; }
    public bool ToTeamsChatCreatedByWayd { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
