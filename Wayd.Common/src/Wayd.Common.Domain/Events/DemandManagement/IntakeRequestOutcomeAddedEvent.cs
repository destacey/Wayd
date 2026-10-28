using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An accepted intake request was recorded as becoming a project.
/// </summary>
/// <remarks>
/// <see cref="TargetIds"/> is every target the request points at afterwards, which a consumer keeping a copy
/// applies whatever order the deliveries arrive in. Summary tier: what a request became is part of where it
/// stands, which every reader of its status is owed.
/// </remarks>
public sealed record IntakeRequestOutcomeAddedEvent : DomainEvent<IntakeRequestOutcomeAddedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    [JsonConstructor]
    public IntakeRequestOutcomeAddedEvent(Guid id, int key, IntakeRequestOutcomeKind kind, Guid targetId, string? note,
        Guid[] targetIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Kind = kind;
        TargetId = targetId;
        Note = note;
        TargetIds = [.. targetIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>What the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; }
    /// <summary>The project added.</summary>
    public Guid TargetId { get; }
    /// <summary>What the outcome covers, in the agent's words.</summary>
    public string? Note { get; }
    /// <summary>Every target the request points at after the change.</summary>
    public Guid[] TargetIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
