using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A different people system's sync took over an employee another people system managed, as happens when
/// an organization changes its people source.
/// </summary>
public sealed record EmployeeTakenOverEvent : DomainEvent<EmployeeTakenOverEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeTakenOverEvent(
        Guid id,
        Connector previousConnector,
        string? previousSystemId,
        Connector connector,
        string? systemId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousConnector = previousConnector;
        PreviousSystemId = previousSystemId;
        Connector = connector;
        SystemId = systemId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The kind of people system that managed the employee before.</summary>
    public Connector PreviousConnector { get; }

    /// <summary>The instance of that system, when known.</summary>
    public string? PreviousSystemId { get; }

    /// <summary>The kind of people system that manages the employee now.</summary>
    public Connector Connector { get; }

    /// <summary>The instance of that system, when known.</summary>
    public string? SystemId { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
