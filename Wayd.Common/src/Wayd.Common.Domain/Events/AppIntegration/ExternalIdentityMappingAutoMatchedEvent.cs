using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A sync matched the external user to an employee by address, or to a different one than before.
/// </summary>
public sealed record ExternalIdentityMappingAutoMatchedEvent : DomainEvent<ExternalIdentityMappingAutoMatchedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingAutoMatchedEvent(Guid id, ExternalIdentityMappingStatus previousStatus, Guid? previousEmployeeId, Guid? employeeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousStatus = previousStatus;
        PreviousEmployeeId = previousEmployeeId;
        EmployeeId = employeeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ExternalIdentityMappingStatus PreviousStatus { get; }
    public Guid? PreviousEmployeeId { get; }
    public Guid? EmployeeId { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
