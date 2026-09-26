using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// The security roles assigned to a user changed.
/// </summary>
/// <remarks>
/// Roles are carried by id, since a role can be renamed. <see cref="Added"/> and <see cref="Removed"/> are the
/// fact; <see cref="Roles"/> is the full set afterwards, for a consumer that keeps a copy.
/// </remarks>
public sealed record ApplicationUserRolesChangedEvent : DomainEvent<ApplicationUserRolesChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationUserRolesChangedEvent(string userId, string[] added, string[] removed, string[] roles, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        UserId = userId;
        Added = [.. added];
        Removed = [.. removed];
        Roles = [.. roles];

        Timestamp = timestamp;
    }

    public string UserId { get; }

    /// <summary>The ids of the roles this change assigned.</summary>
    public string[] Added { get; }

    /// <summary>The ids of the roles this change removed.</summary>
    public string[] Removed { get; }

    /// <summary>The ids of the roles the user holds after the change.</summary>
    public string[] Roles { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationUser";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(UserId);
}
