using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk was given an estimation scale, moved to another scale or version, or stopped estimating its
/// requests.
/// </summary>
/// <remarks>
/// One event rather than a type per verb, as for <see cref="IntakeDeskScoringModelChangedEvent"/>. The desk
/// pins a version, so each end names the scale and the version; a null <see cref="ToScaleVersionId"/> stops
/// new estimates on the desk. Both scales are named as well as identified, so the entry stays readable after
/// either is renamed.
/// </remarks>
public sealed record IntakeDeskEstimationScaleChangedEvent : DomainEvent<IntakeDeskEstimationScaleChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IntakeDeskEstimationScaleChangedEvent(
        Guid id,
        int key,
        Guid? fromScaleId,
        string? fromScaleName,
        Guid? fromScaleVersionId,
        int? fromScaleVersionNumber,
        Guid? toScaleId,
        string? toScaleName,
        Guid? toScaleVersionId,
        int? toScaleVersionNumber,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromScaleId = fromScaleId;
        FromScaleName = fromScaleName;
        FromScaleVersionId = fromScaleVersionId;
        FromScaleVersionNumber = fromScaleVersionNumber;
        ToScaleId = toScaleId;
        ToScaleName = toScaleName;
        ToScaleVersionId = toScaleVersionId;
        ToScaleVersionNumber = toScaleVersionNumber;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The scale the desk estimated on before, or null when it had none.</summary>
    public Guid? FromScaleId { get; }

    /// <summary>The name of <see cref="FromScaleId"/> when the change was made.</summary>
    public string? FromScaleName { get; }

    /// <summary>The version of <see cref="FromScaleId"/> the desk was pinned to.</summary>
    public Guid? FromScaleVersionId { get; }

    /// <summary>The number of <see cref="FromScaleVersionId"/> within its scale.</summary>
    public int? FromScaleVersionNumber { get; }

    /// <summary>The scale the desk estimates on now, or null once it stops estimating.</summary>
    public Guid? ToScaleId { get; }

    /// <summary>The name of <see cref="ToScaleId"/> when the change was made.</summary>
    public string? ToScaleName { get; }

    /// <summary>The version of <see cref="ToScaleId"/> the desk is pinned to now.</summary>
    public Guid? ToScaleVersionId { get; }

    /// <summary>The number of <see cref="ToScaleVersionId"/> within its scale.</summary>
    public int? ToScaleVersionNumber { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
