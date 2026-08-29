using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Identity;

public abstract record ApplicationUserEvent : DomainEvent
{
    public string UserId { get; set; } = default!;

    protected ApplicationUserEvent(string userId, EventActor actor, Instant timestamp)
        : base(actor) =>
        (UserId, Timestamp) = (userId, timestamp);
}

public record ApplicationUserCreatedEvent : ApplicationUserEvent
{
    public ApplicationUserCreatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}

public record ApplicationUserUpdatedEvent : ApplicationUserEvent
{
    public bool RolesUpdated { get; set; }

    public ApplicationUserUpdatedEvent(string userId, EventActor actor, Instant timestamp, bool rolesUpdated = false)
        : base(userId, actor, timestamp) =>
        RolesUpdated = rolesUpdated;
}

public record ApplicationUserActivatedEvent : ApplicationUserEvent
{
    public ApplicationUserActivatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}

public record ApplicationUserDeactivatedEvent : ApplicationUserEvent
{
    public ApplicationUserDeactivatedEvent(string userId, EventActor actor, Instant timestamp)
        : base(userId, actor, timestamp)
    {
    }
}
