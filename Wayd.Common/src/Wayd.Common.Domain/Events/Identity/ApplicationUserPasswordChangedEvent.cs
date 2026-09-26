using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A local user changed their own password.
/// </summary>
public sealed record ApplicationUserPasswordChangedEvent : DomainEvent<ApplicationUserPasswordChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserPasswordChangedEvent(string userId, EventActor actor, Instant timestamp)
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
