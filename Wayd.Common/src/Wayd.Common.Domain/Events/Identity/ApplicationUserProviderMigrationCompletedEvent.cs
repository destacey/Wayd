using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A staged identity provider migration completed: the user signed in through its target provider and now
/// signs in only through it.
/// </summary>
public sealed record ApplicationUserProviderMigrationCompletedEvent : DomainEvent<ApplicationUserProviderMigrationCompletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserProviderMigrationCompletedEvent(string userId, string fromProvider, string toProvider, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        FromProvider = fromProvider;
        ToProvider = toProvider;

        Timestamp = timestamp;
    }

    public string UserId { get; }
    public string FromProvider { get; }
    public string ToProvider { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
