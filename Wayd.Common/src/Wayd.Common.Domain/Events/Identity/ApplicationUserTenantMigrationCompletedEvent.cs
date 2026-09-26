using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// A staged tenant migration completed: the user signed in from its target tenant and their Microsoft Entra ID
/// sign-in was rebound to it.
/// </summary>
/// <remarks>
/// The sign-in it replaced, if any, is the deactivated <c>UserIdentity</c> row the rebind left behind, which
/// records its tenant and when it was unlinked.
/// </remarks>
public sealed record ApplicationUserTenantMigrationCompletedEvent : DomainEvent<ApplicationUserTenantMigrationCompletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserTenantMigrationCompletedEvent(string userId, string tenantId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        TenantId = tenantId;

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>The tenant the user now signs in from.</summary>
    public string TenantId { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
