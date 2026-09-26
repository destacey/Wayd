using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user was staged to move to another identity provider. Their next sign-in through that provider completes it.
/// </summary>
public sealed record ApplicationUserProviderMigrationStagedEvent : DomainEvent<ApplicationUserProviderMigrationStagedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserProviderMigrationStagedEvent(string userId, string targetProvider, string? previousTargetProvider, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        TargetProvider = targetProvider;
        PreviousTargetProvider = previousTargetProvider;

        Timestamp = timestamp;
    }

    public string UserId { get; }
    public string TargetProvider { get; }

    /// <summary>The target of a staged migration this one replaced; null when none was staged.</summary>
    public string? PreviousTargetProvider { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
