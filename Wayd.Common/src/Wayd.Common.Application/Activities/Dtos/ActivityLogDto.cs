using Mapster;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Activities;

namespace Wayd.Common.Application.Activities.Dtos;

public sealed record ActivityLogDto : IMapFrom<ActivityLogEntry>
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string DomainArea { get; init; }
    public required string AggregateType { get; init; }
    public Guid AggregateId { get; init; }
    public EventActorKind ActorKind { get; init; }
    public EmployeeNavigationDto? Employee { get; init; }
    public Instant Timestamp { get; init; }
    public string? CorrelationId { get; init; }
    public required string Payload { get; init; }
    public string? Summary { get; init; }
}

