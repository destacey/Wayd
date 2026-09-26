using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A staged identity provider migration was canceled before the user signed in through its target provider.
/// </summary>
public sealed record ApplicationUserProviderMigrationCanceledEvent : DomainEvent<ApplicationUserProviderMigrationCanceledEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserProviderMigrationCanceledEvent(string userId, string targetProvider, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        TargetProvider = targetProvider;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>The provider the canceled migration would have moved the user to.</summary>
    public string TargetProvider { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
