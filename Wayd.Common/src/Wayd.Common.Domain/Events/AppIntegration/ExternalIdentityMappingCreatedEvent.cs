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
/// cannot be corrected. Nor the external id, which a mapping seeded before syncs reported ids holds as the person's
/// address until a sync re-keys it.
/// </remarks>
public sealed record ExternalIdentityMappingCreatedEvent : DomainEvent<ExternalIdentityMappingCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public ExternalIdentityMappingCreatedEvent(Guid id, Connector connector, Guid connectionId, Guid? employeeId, ExternalIdentityMappingStatus status, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Connector = connector;
        ConnectionId = connectionId;
        EmployeeId = employeeId;
        Status = status;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public Connector Connector { get; }
    public Guid ConnectionId { get; }

    public Guid? EmployeeId { get; }
    public ExternalIdentityMappingStatus Status { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
