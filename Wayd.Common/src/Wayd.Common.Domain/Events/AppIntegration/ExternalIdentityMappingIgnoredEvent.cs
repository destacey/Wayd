using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An admin marked the external user as one that will never have an employee. A sync leaves the decision alone.
/// </summary>
public sealed record ExternalIdentityMappingIgnoredEvent : DomainEvent<ExternalIdentityMappingIgnoredEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingIgnoredEvent(Guid id, ExternalIdentityMappingStatus previousStatus, Guid? previousEmployeeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        PreviousStatus = previousStatus;
        PreviousEmployeeId = previousEmployeeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ExternalIdentityMappingStatus PreviousStatus { get; }
    public Guid? PreviousEmployeeId { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalIdentityMapping";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
