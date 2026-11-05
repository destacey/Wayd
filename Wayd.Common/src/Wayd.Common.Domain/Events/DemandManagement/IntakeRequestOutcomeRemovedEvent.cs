using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An outcome was taken off an accepted intake request: it no longer counts the project or work item as
/// what it became.
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
        : base(actor, "1.1")
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
    /// <summary>The project or work item removed; <see cref="Kind"/> says which.</summary>
    public Guid TargetId { get; }
    /// <summary>The removed outcome's note.</summary>
    public string? Note { get; }
    /// <summary>
    /// Every target the request points at after the change, projects and work items together; since 1.1 it
    /// may hold work item ids, which no 1.0 payload carries.
    /// </summary>
    public Guid[] TargetIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
