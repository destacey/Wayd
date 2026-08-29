using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Identity;

public abstract record ApplicationRoleEvent : DomainEvent
{
    public string RoleId { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    protected ApplicationRoleEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(actor) =>
        (RoleId, RoleName, Timestamp) = (roleId, roleName, timestamp);
}

public record ApplicationRoleCreatedEvent : ApplicationRoleEvent
{
    public ApplicationRoleCreatedEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(roleId, roleName, actor, timestamp)
    {
    }
}

public record ApplicationRoleUpdatedEvent : ApplicationRoleEvent
{
    public bool PermissionsUpdated { get; set; }

    public ApplicationRoleUpdatedEvent(string roleId, string roleName, EventActor actor, Instant timestamp, bool permissionsUpdated = false)
        : base(roleId, roleName, actor, timestamp) =>
        PermissionsUpdated = permissionsUpdated;
}

public record ApplicationRoleDeletedEvent : ApplicationRoleEvent
{
    public bool PermissionsUpdated { get; set; }

    public ApplicationRoleDeletedEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(roleId, roleName, actor, timestamp)
    {
    }
}
