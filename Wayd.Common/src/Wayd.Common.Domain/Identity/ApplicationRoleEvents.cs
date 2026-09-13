using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Identity;

public abstract record ApplicationRoleEvent<TSelf> : DomainEvent<TSelf>
    where TSelf : ApplicationRoleEvent<TSelf>, IDomainEventDescriptor
{
    public string RoleId { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    protected ApplicationRoleEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(actor, "1.0") =>
        (RoleId, RoleName, Timestamp) = (roleId, roleName, timestamp);
}

public record ApplicationRoleCreatedEvent : ApplicationRoleEvent<ApplicationRoleCreatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public ApplicationRoleCreatedEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(roleId, roleName, actor, timestamp)
    {
    }
}

public record ApplicationRoleUpdatedEvent : ApplicationRoleEvent<ApplicationRoleUpdatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public bool PermissionsUpdated { get; set; }

    public ApplicationRoleUpdatedEvent(string roleId, string roleName, EventActor actor, Instant timestamp, bool permissionsUpdated = false)
        : base(roleId, roleName, actor, timestamp) =>
        PermissionsUpdated = permissionsUpdated;
}

public record ApplicationRoleDeletedEvent : ApplicationRoleEvent<ApplicationRoleDeletedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public bool PermissionsUpdated { get; set; }

    public ApplicationRoleDeletedEvent(string roleId, string roleName, EventActor actor, Instant timestamp)
        : base(roleId, roleName, actor, timestamp)
    {
    }
}
