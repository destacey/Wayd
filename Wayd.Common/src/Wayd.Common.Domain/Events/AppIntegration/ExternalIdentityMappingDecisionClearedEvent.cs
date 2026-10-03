using System.Text.Json.Serialization;
using Wayd.Common.Domain.AppIntegrations;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An admin cleared their decision, returning the external user to the review queue for the next sync to match.
/// </summary>
public sealed record ExternalIdentityMappingDecisionClearedEvent : DomainEvent<ExternalIdentityMappingDecisionClearedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalIdentityMappingDecisionClearedEvent(Guid id, ExternalIdentityMappingStatus previousStatus, Guid? previousEmployeeId, EventActor actor, Instant timestamp)
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
