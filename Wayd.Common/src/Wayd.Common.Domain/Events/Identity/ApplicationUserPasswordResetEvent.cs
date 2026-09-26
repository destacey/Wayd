using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// An administrator set a new password for a local user, who must change it at their next sign-in.
/// </summary>
public sealed record ApplicationUserPasswordResetEvent : DomainEvent<ApplicationUserPasswordResetEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserPasswordResetEvent(string userId, bool clearedLockout, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        ClearedLockout = clearedLockout;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>Whether the reset also ended a lockout the user was under.</summary>
    public bool ClearedLockout { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
