using Mapster;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Activities;

namespace Wayd.Common.Application.Activities.Dtos;

public sealed record ActivityLogDto : IMapFrom<ActivityLogEntry>
{
    /// <summary>
    /// The entry's key, used only to tell one row from another. It is not the id of the event the entry records.
    /// </summary>
    /// <remarks>
    /// The event's own id is <see cref="ActivityLogEntry.EventId"/> and is not published: it identifies the event,
    /// not the entry, and nothing outside the log has a use for it.
    /// </remarks>
    public long Id { get; init; }

    public required string EventType { get; init; }
    public ActivityCategory Category { get; init; }
    public required string DomainArea { get; init; }
    public required string AggregateType { get; init; }
    public Guid AggregateId { get; init; }
    public EventActorKind ActorKind { get; init; }
    public EmployeeNavigationDto? Employee { get; init; }

    /// <summary>
    /// The OAuth client the change was made through, such as an agent connected to the hosted MCP endpoint: its
    /// <c>client_id</c>, the URL of its metadata document, whose host identifies it. Null for a change made directly.
    /// </summary>
    public string? ClientId { get; init; }
    public Instant Timestamp { get; init; }
    public string? CorrelationId { get; init; }
    public string EventVersion { get; init; } = "1.0";
    public required string Payload { get; init; }
    public string? Summary { get; init; }

    /// <summary>
    /// Whether the entry was raised on another record and is listed here because its event names this one.
    /// <c>aggregateType</c> and <c>aggregateId</c> are then that other record.
    /// </summary>
    public bool IsRelated { get; init; }

    /// <summary>
    /// The record a related entry was raised on. Null when the entry is not related, or when the record could
    /// not be resolved, typically because it has since been removed.
    /// </summary>
    /// <remarks>
    /// Left to the module reading the log because the log holds ids only.
    /// </remarks>
    public NavigationDto? RaisedOn { get; init; }
}

