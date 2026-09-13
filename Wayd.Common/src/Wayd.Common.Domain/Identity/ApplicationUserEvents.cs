using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Identity;

public abstract record ApplicationUserEvent<TSelf> : DomainEvent<TSelf>
    where TSelf : ApplicationUserEvent<TSelf>, IDomainEventDescriptor
{
    public string UserId { get; set; } = default!;

    protected ApplicationUserEvent(string userId, EventActor actor, Instant timestamp)
        : base(actor, "1.0") =>
        (UserId, Timestamp) = (userId, timestamp);
}

public record ApplicationUserCreatedEvent : ApplicationUserEvent<ApplicationUserCreatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public ApplicationUserCreatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}

public record ApplicationUserUpdatedEvent : ApplicationUserEvent<ApplicationUserUpdatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public bool RolesUpdated { get; set; }

    public ApplicationUserUpdatedEvent(string userId, EventActor actor, Instant timestamp, bool rolesUpdated = false)
        : base(userId, actor, timestamp) =>
        RolesUpdated = rolesUpdated;
}

public record ApplicationUserActivatedEvent : ApplicationUserEvent<ApplicationUserActivatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ApplicationUserActivatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}

public record ApplicationUserDeactivatedEvent : ApplicationUserEvent<ApplicationUserDeactivatedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public ApplicationUserDeactivatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}
