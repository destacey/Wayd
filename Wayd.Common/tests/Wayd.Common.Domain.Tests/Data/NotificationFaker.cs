using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Notifications;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

/// <summary>
/// Builds unread notifications, including the id and read instant only the database sets.
/// </summary>
/// <remarks>
/// Skips <see cref="Notification.Create"/>, so the owner type need not be registered; tests of creation itself
/// call <see cref="Notification.Create"/>.
/// </remarks>
public sealed class NotificationFaker : PrivateConstructorFaker<Notification>
{
    public NotificationFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Long(1, long.MaxValue));
        RuleFor(x => x.RecipientEmployeeId, f => f.Random.Guid());
        RuleFor(x => x.OwnerType, "test.notification.record");
        RuleFor(x => x.OwnerId, f => f.Random.Guid());
        RuleFor(x => x.EventId, f => f.Random.Guid());
        RuleFor(x => x.Event, NotificationEvent.StatusChanged);
        RuleFor(x => x.ActorKind, EventActorKind.User);
        RuleFor(x => x.ActorEmployeeId, (Guid?)null);
        RuleFor(x => x.OccurredOn, Instant.FromUtc(2026, 10, 1, 9, 0));
        RuleFor(x => x.Reasons, NotificationReason.Following);
        RuleFor(x => x.ReadOn, (Instant?)null);
    }
}

/// <summary>Per-property builders for <see cref="NotificationFaker"/>.</summary>
public static class NotificationFakerExtensions
{
    public static NotificationFaker WithId(this NotificationFaker faker, long id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static NotificationFaker WithRecipientEmployeeId(this NotificationFaker faker, Guid recipientEmployeeId)
    {
        faker.RuleFor(x => x.RecipientEmployeeId, recipientEmployeeId);
        return faker;
    }

    public static NotificationFaker WithOwner(this NotificationFaker faker, string ownerType, Guid ownerId)
    {
        faker.RuleFor(x => x.OwnerType, ownerType);
        faker.RuleFor(x => x.OwnerId, ownerId);
        return faker;
    }

    public static NotificationFaker WithEvent(this NotificationFaker faker, NotificationEvent @event)
    {
        faker.RuleFor(x => x.Event, @event);
        return faker;
    }

    public static NotificationFaker WithActorEmployeeId(this NotificationFaker faker, Guid? actorEmployeeId)
    {
        faker.RuleFor(x => x.ActorEmployeeId, actorEmployeeId);
        return faker;
    }

    public static NotificationFaker WithOccurredOn(this NotificationFaker faker, Instant occurredOn)
    {
        faker.RuleFor(x => x.OccurredOn, occurredOn);
        return faker;
    }

    public static NotificationFaker WithReasons(this NotificationFaker faker, NotificationReason reasons)
    {
        faker.RuleFor(x => x.Reasons, reasons);
        return faker;
    }

    public static NotificationFaker WithReadOn(this NotificationFaker faker, Instant? readOn)
    {
        faker.RuleFor(x => x.ReadOn, readOn);
        return faker;
    }
}
