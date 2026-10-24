using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Someone was given access to an intake request as a participant.
/// </summary>
/// <remarks>
/// Substance tier, because who has access to a request is part of its private side.
/// </remarks>
public sealed record IntakeRequestParticipantAddedEvent : DomainEvent<IntakeRequestParticipantAddedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Substance;

    public IntakeRequestParticipantAddedEvent(Guid id, int key, Guid employeeId, IReadOnlyList<Guid> participantIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        EmployeeId = employeeId;
        ParticipantIds = participantIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>The employee added.</summary>
    public Guid EmployeeId { get; }
    /// <summary>Every participant after the change, excluding the requester and submitter, who are implicit.</summary>
    public IReadOnlyList<Guid> ParticipantIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
