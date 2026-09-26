using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A user's next Microsoft Entra ID sign-in was pointed at a tenant: a tenant migration, or the first sign-in
/// tenant of a user who has no Entra identity yet. The next sign-in from that tenant completes it.
/// </summary>
public sealed record ApplicationUserTenantMigrationStagedEvent : DomainEvent<ApplicationUserTenantMigrationStagedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserTenantMigrationStagedEvent(string userId, string targetTenantId, string? previousTargetTenantId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        TargetTenantId = targetTenantId;
        PreviousTargetTenantId = previousTargetTenantId;

        Timestamp = timestamp;
    }

    public string UserId { get; }
    public string TargetTenantId { get; }

    /// <summary>The target of a staged migration this one replaced; null when none was staged.</summary>
    public string? PreviousTargetTenantId { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
