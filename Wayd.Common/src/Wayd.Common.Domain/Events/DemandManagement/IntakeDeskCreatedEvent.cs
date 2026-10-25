using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk was defined, with the visibility and members it started with.
/// </summary>
/// <remarks>
/// Carries the visibility and members because together they decide who could read which requests from
/// this moment on, so a later change has a starting point to be compared with.
/// </remarks>
public sealed record IntakeDeskCreatedEvent : DomainEvent<IntakeDeskCreatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IntakeDeskCreatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        IntakeDeskVisibility visibility,
        bool isRequesterSelectable,
        bool isOpen,
        bool requesterMayAddParticipants,
        IReadOnlyList<IntakeDeskMemberDetails> members,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Visibility = visibility;
        IsRequesterSelectable = isRequesterSelectable;
        IsOpen = isOpen;
        RequesterMayAddParticipants = requesterMayAddParticipants;
        Members = members;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    /// <summary>The requester-facing description of what to bring to the desk.</summary>
    public string Description { get; }
    public IntakeDeskVisibility Visibility { get; }
    public bool IsRequesterSelectable { get; }
    public bool IsOpen { get; }
    public bool RequesterMayAddParticipants { get; }

    /// <summary>The employees named on the desk when it was defined, with their roles.</summary>
    public IReadOnlyList<IntakeDeskMemberDetails> Members { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
