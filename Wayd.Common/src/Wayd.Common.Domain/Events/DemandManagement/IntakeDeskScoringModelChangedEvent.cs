using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk was given a scoring model, moved to another, or stopped scoring its requests.
/// </summary>
/// <remarks>
/// One event rather than a type per verb: what a desk scores against is a state, and assigning, swapping and
/// clearing all leave it in one. A null <see cref="ToScoringModelId"/> stops new scoring on the desk. Scores
/// already recorded are unaffected, since each froze the model it was scored against. Both ends are named as
/// well as identified, so the entry stays readable after either model is renamed or archived.
/// </remarks>
public sealed record IntakeDeskScoringModelChangedEvent : DomainEvent<IntakeDeskScoringModelChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IntakeDeskScoringModelChangedEvent(
        Guid id,
        int key,
        Guid? fromScoringModelId,
        string? fromScoringModelName,
        Guid? toScoringModelId,
        string? toScoringModelName,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromScoringModelId = fromScoringModelId;
        FromScoringModelName = fromScoringModelName;
        ToScoringModelId = toScoringModelId;
        ToScoringModelName = toScoringModelName;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The model the desk scored against before, or null when it had none.</summary>
    public Guid? FromScoringModelId { get; }
    public string? FromScoringModelName { get; }

    /// <summary>The model the desk scores against now, or null once it stops scoring.</summary>
    public Guid? ToScoringModelId { get; }
    public string? ToScoringModelName { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
