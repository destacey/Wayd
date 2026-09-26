using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user was locked out after too many failed sign-in attempts.
/// </summary>
public sealed record ApplicationUserLockedOutEvent : DomainEvent<ApplicationUserLockedOutEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ApplicationUserLockedOutEvent(string userId, Instant lockedUntil, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        LockedUntil = lockedUntil;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>When the lockout expires on its own, unless an administrator ends it first.</summary>
    public Instant LockedUntil { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
