using Mapster;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Activities;

namespace Wayd.Common.Application.Activities.Dtos;

public sealed record ActivityLogDto : IMapFrom<ActivityLogEntry>
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public ActivityCategory Category { get; init; }
    public required string DomainArea { get; init; }
    public required string AggregateType { get; init; }
    public Guid AggregateId { get; init; }
    public EventActorKind ActorKind { get; init; }
    public EmployeeNavigationDto? Employee { get; init; }
    public Instant Timestamp { get; init; }
    public string? CorrelationId { get; init; }
    public string EventVersion { get; init; } = "1.0";
    public required string Payload { get; init; }
    public string? Summary { get; init; }

    /// <summary>
    /// Whether the entry was raised on another record and is listed here because its event names this one.
    /// <see cref="AggregateType"/> and <see cref="AggregateId"/> are then that other record.
    /// </summary>
    public bool IsRelated { get; init; }

    /// <summary>
    /// The record a related entry was raised on, where the module reading the log resolved it.
    /// </summary>
    /// <remarks>
    /// Left to the module because the log holds ids only. Null on a related entry means the record could not
    /// be resolved, typically because it has since been removed.
    /// </remarks>
    public NavigationDto? RaisedOn { get; init; }
}

