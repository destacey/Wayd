using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user who signed in through an external identity provider was converted to a local Wayd account with a
/// password, which they must change at their next sign-in.
/// </summary>
public sealed record ApplicationUserConvertedToLocalAccountEvent : DomainEvent<ApplicationUserConvertedToLocalAccountEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserConvertedToLocalAccountEvent(string userId, string fromProvider, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        FromProvider = fromProvider;

        Timestamp = timestamp;
    }

    public string UserId { get; }
    public string FromProvider { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
