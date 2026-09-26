using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An administrator ended a user's lockout before it expired.
/// </summary>
public sealed record ApplicationUserUnlockedEvent : DomainEvent<ApplicationUserUnlockedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ApplicationUserUnlockedEvent(string userId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
