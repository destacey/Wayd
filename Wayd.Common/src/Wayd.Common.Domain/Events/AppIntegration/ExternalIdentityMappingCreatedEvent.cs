using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A sync met a user in an external system for the first time, and either matched them to an employee by
/// address or left them for an admin to map.
/// </summary>
/// <remarks>
/// Carries none of the external profile: an address, display name or handle is a person's, and the activity log
/// cannot be corrected.
/// </remarks>
public sealed record ExternalIdentityMappingCreatedEvent : DomainEvent<ExternalIdentityMappingCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public ExternalIdentityMappingCreatedEvent(Guid id, Connector connector, Guid connectionId, string externalId, Guid? employeeId, ExternalIdentityMappingStatus status, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Connector = connector;
        ConnectionId = connectionId;
        ExternalId = externalId;
        EmployeeId = employeeId;
        Status = status;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public Connector Connector { get; }
    public Guid ConnectionId { get; }

    /// <summary>The external system's stable id for the user.</summary>
    public string ExternalId { get; }

    public Guid? EmployeeId { get; }
    public ExternalIdentityMappingStatus Status { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
