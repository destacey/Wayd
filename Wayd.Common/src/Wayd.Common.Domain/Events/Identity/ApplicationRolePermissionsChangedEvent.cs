using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// The permissions a role grants changed. Supersedes <see cref="Domain.Identity.ApplicationRoleUpdatedEvent"/> for these changes.
/// </summary>
/// <remarks>
/// <see cref="Added"/> and <see cref="Removed"/> are the fact; <see cref="Permissions"/> is the full set
/// afterwards, for a consumer that keeps a copy.
/// </remarks>
public sealed record ApplicationRolePermissionsChangedEvent : DomainEvent<ApplicationRolePermissionsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public ApplicationRolePermissionsChangedEvent(string roleId, string[] added, string[] removed, string[] permissions, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        RoleId = roleId;
        Added = [.. added];
        Removed = [.. removed];
        Permissions = [.. permissions];

        Timestamp = timestamp;
    }

    public string RoleId { get; }

    /// <summary>The permissions this change granted.</summary>
    public string[] Added { get; }

    /// <summary>The permissions this change revoked.</summary>
    public string[] Removed { get; }

    /// <summary>The permissions the role grants after the change.</summary>
    public string[] Permissions { get; }

    [JsonIgnore]
    public string AggregateType => "ApplicationRole";
    [JsonIgnore]
    public Guid AggregateId => Guid.Parse(RoleId);
}
