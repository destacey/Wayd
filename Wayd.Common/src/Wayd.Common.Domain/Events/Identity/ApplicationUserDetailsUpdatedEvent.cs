using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user's name, email address or phone number was edited, by the user, an administrator or a people sync.
/// </summary>
/// <remarks>
/// Carries no values, before or after: every field it covers is personal data, which an append-only log
/// could never correct or erase. A reader resolves the user's current details by <see cref="UserId"/>.
/// </remarks>
public sealed record ApplicationUserDetailsUpdatedEvent : DomainEvent<ApplicationUserDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserDetailsUpdatedEvent(string userId, EventActor actor, Instant timestamp)
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
