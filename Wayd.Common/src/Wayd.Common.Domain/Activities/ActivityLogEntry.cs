using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Activities;

/// <summary>
/// An immutable, append-only record of a domain business event.
/// Captures what occurred, on what aggregate, by whom, and when.
/// </summary>
public sealed class ActivityLogEntry : BaseEntity
{
    private ActivityLogEntry() { }

    public ActivityLogEntry(
        Guid id,
        string eventType,
        string domainArea,
        string aggregateType,
        Guid aggregateId,
        EventActor actor,
        Instant timestamp,
        string? correlationId,
        string payload,
        string? summary = null)
    {
        Id = Guard.Against.Default(id, nameof(id));
        EventType = Guard.Against.NullOrWhiteSpace(eventType, nameof(eventType)).Trim();
        DomainArea = Guard.Against.NullOrWhiteSpace(domainArea, nameof(domainArea)).Trim();
        AggregateType = Guard.Against.NullOrWhiteSpace(aggregateType, nameof(aggregateType)).Trim();
        AggregateId = Guard.Against.Default(aggregateId, nameof(aggregateId));

        Guard.Against.Null(actor, nameof(actor));
        ActorKind = actor.Kind;
        UserId = actor.UserId;
        EmployeeId = actor.EmployeeId;

        Timestamp = timestamp;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        Payload = Guard.Against.Null(payload, nameof(payload));
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }

    /// <summary>The runtime type name of the raised domain event.</summary>
    public string EventType { get; private init; } = default!;

    /// <summary>The bounded context domain area (e.g. "Ppm", "Organization", "Planning", "ProductManagement", "Work").</summary>
    public string DomainArea { get; private init; } = default!;

    /// <summary>The aggregate entity type (e.g. "Project", "Team", "Product", "Version").</summary>
    public string AggregateType { get; private init; } = default!;

    /// <summary>The identifier of the target entity.</summary>
    public Guid AggregateId { get; private init; }

    /// <summary>The mechanism that performed the change.</summary>
    public EventActorKind ActorKind { get; private init; }

    /// <summary>The account that originated the change, where there is one.</summary>
    public string? UserId { get; private init; }

    /// <summary>The employee behind the change, where there is one.</summary>
    public Guid? EmployeeId { get; private init; }

    /// <summary>The employee navigation entity.</summary>
    public Wayd.Common.Domain.Employees.Employee? Employee { get; private init; }

    /// <summary>When the event occurred in the domain.</summary>
    public Instant Timestamp { get; private init; }

    /// <summary>Ties this event back to the request or background operation that caused it.</summary>
    public string? CorrelationId { get; private init; }

    /// <summary>The serialized JSON representation of the domain event payload.</summary>
    public string Payload { get; private init; } = default!;

    /// <summary>A human-readable summary of the event.</summary>
    public string? Summary { get; private init; }
}

