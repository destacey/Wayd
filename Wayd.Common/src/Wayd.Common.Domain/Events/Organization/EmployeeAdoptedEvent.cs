using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A people sync took over an employee that Wayd owned, so its details now come from the sync's system.
/// </summary>
public sealed record EmployeeAdoptedEvent : DomainEvent<EmployeeAdoptedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EmployeeAdoptedEvent(
        Guid id,
        Ownership previousOwnership,
        Connector? previousConnector,
        string? previousSystemId,
        LocalDate? previousAccessEndDate,
        Ownership ownership,
        Connector connector,
        string? systemId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousOwnership = previousOwnership;
        PreviousConnector = previousConnector;
        PreviousSystemId = previousSystemId;
        PreviousAccessEndDate = previousAccessEndDate;
        Ownership = ownership;
        Connector = connector;
        SystemId = systemId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>Who wrote the employee before; always <see cref="Enums.Ownership.Owned"/> for an adoption.</summary>
    public Ownership PreviousOwnership { get; }

    /// <summary>The system the employee was released from, when it kept a reference.</summary>
    public Connector? PreviousConnector { get; }

    /// <summary>The instance of that system, when known.</summary>
    public string? PreviousSystemId { get; }

    /// <summary>The access end date the employee had while Wayd owned it; adoption clears it.</summary>
    public LocalDate? PreviousAccessEndDate { get; }

    /// <summary>Who writes the employee now; always <see cref="Enums.Ownership.Managed"/>.</summary>
    public Ownership Ownership { get; }

    /// <summary>The kind of people system the employee now comes from.</summary>
    public Connector Connector { get; }

    /// <summary>The instance of that system, when known.</summary>
    public string? SystemId { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
