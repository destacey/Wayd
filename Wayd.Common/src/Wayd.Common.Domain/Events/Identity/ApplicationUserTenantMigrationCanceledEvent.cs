using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A staged tenant migration was canceled before the user signed in from its target tenant.
/// </summary>
public sealed record ApplicationUserTenantMigrationCanceledEvent : DomainEvent<ApplicationUserTenantMigrationCanceledEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserTenantMigrationCanceledEvent(string userId, string targetTenantId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        TargetTenantId = targetTenantId;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>The tenant the canceled migration would have moved the user to.</summary>
    public string TargetTenantId { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
