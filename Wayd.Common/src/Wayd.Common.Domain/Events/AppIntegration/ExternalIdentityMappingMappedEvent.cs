using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An admin mapped the external user to an employee. A sync leaves the decision alone.
/// </summary>
public sealed record ExternalIdentityMappingMappedEvent : DomainEvent<ExternalIdentityMappingMappedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingMappedEvent(Guid id, ExternalIdentityMappingStatus previousStatus, Guid? previousEmployeeId, Guid? employeeId, EventActor actor, Instant timestamp)
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
