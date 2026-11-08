using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// The project deliverables an existing project scope outcome is mapped to changed: those whose acceptance
/// the request waits for.
/// </summary>
/// <remarks>
/// Carries what was added and removed and <see cref="DeliverableIds"/>, the outcome's whole mapping afterwards,
/// which a consumer applies whatever order the deliveries arrive in. Summary tier, like the outcome itself.
/// </remarks>
public sealed record IntakeRequestOutcomeDeliverablesChangedEvent : DomainEvent<IntakeRequestOutcomeDeliverablesChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    [JsonConstructor]
    public IntakeRequestOutcomeDeliverablesChangedEvent(Guid id, int key, Guid targetId, Guid[] added, Guid[] removed,
        Guid[] deliverableIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        TargetId = targetId;
        Added = [.. added];
        Removed = [.. removed];
        DeliverableIds = [.. deliverableIds];

        Timestamp = timestamp;
    }

    /// <summary>The request.</summary>
    public Guid Id { get; }
    /// <summary>The request's number.</summary>
    public int Key { get; }
    /// <summary>The project the outcome points at.</summary>
    public Guid TargetId { get; }
    /// <summary>The deliverables mapped by the change.</summary>
    public Guid[] Added { get; }
    /// <summary>The deliverables no longer mapped after the change.</summary>
    public Guid[] Removed { get; }
    /// <summary>Every deliverable the outcome is mapped to after the change.</summary>
    public Guid[] DeliverableIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
