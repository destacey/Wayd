using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Enums.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// Tracking began for an external identity mapping that existed before <see cref="ExternalIdentityMappingCreatedEvent"/>
/// was recorded. Carries that event's payload, describing the mapping as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record ExternalIdentityMappingBaselinedEvent : BaselineEvent<ExternalIdentityMappingBaselinedEvent, ExternalIdentityMappingCreatedEvent>
{
    public ExternalIdentityMappingBaselinedEvent(Guid id, Connector connector, Guid connectionId, Guid? employeeId, ExternalIdentityMappingStatus status, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("ExternalIdentityMapping", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Connector = connector;
        ConnectionId = connectionId;
        EmployeeId = employeeId;
        Status = status;
    }

    public Guid Id { get; }
    public Connector Connector { get; }
    public Guid ConnectionId { get; }
    public Guid? EmployeeId { get; }
    public ExternalIdentityMappingStatus Status { get; }
}
