using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk was defined, with the visibility and reviewers it started with.
/// </summary>
/// <remarks>
/// Carries the visibility and reviewers because together they decide who could read which requests from
/// this moment on, so a later change has a starting point to be compared with.
/// </remarks>
public sealed record IntakeDeskCreatedEvent : DomainEvent<IntakeDeskCreatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IntakeDeskCreatedEvent(
        Guid id,
        int key,
        string name,
        IntakeDeskVisibility visibility,
        bool isRequesterSelectable,
        bool isOpen,
        bool requesterMayAddParticipants,
        IReadOnlyList<Guid> reviewerIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Visibility = visibility;
        IsRequesterSelectable = isRequesterSelectable;
        IsOpen = isOpen;
        RequesterMayAddParticipants = requesterMayAddParticipants;
        ReviewerIds = reviewerIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IntakeDeskVisibility Visibility { get; }
    public bool IsRequesterSelectable { get; }
    public bool IsOpen { get; }
    public bool RequesterMayAddParticipants { get; }

    /// <summary>The employees named as reviewers when the desk was defined.</summary>
    public IReadOnlyList<Guid> ReviewerIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
