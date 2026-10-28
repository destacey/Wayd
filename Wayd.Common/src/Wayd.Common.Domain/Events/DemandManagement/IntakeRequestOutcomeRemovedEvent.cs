using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An outcome was taken off an accepted intake request: it no longer counts the project as what
/// it became.
/// </summary>
/// <remarks>
/// Carries the removed outcome as it was, and <see cref="TargetIds"/>, every target the request points at
/// afterwards. Summary tier, like the outcome itself.
/// </remarks>
public sealed record IntakeRequestOutcomeRemovedEvent : DomainEvent<IntakeRequestOutcomeRemovedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    [JsonConstructor]
    public IntakeRequestOutcomeRemovedEvent(Guid id, int key, IntakeRequestOutcomeKind kind, Guid targetId, string? note,
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
    /// <summary>What the removed outcome said the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; }
    /// <summary>The project removed.</summary>
    public Guid TargetId { get; }
    /// <summary>The removed outcome's note.</summary>
    public string? Note { get; }
    /// <summary>Every target the request points at after the change.</summary>
    public Guid[] TargetIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
