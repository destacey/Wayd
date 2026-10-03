using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// The external user's address stopped resolving to an employee, so a sync returned them to the review queue.
/// </summary>
public sealed record ExternalIdentityMappingUnmatchedEvent : DomainEvent<ExternalIdentityMappingUnmatchedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingUnmatchedEvent(Guid id, ExternalIdentityMappingStatus previousStatus, Guid? previousEmployeeId, EventActor actor, Instant timestamp)
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
