using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Notifications;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// The read tier a reader must hold on an intake request to be told of each kind of change to it.
/// </summary>
/// <remarks>
/// A notification stores what happened as a <see cref="NotificationEvent"/>, not the domain event's type, so
/// the tier the event declares is restated here for each member the request's notifications use. Each must
/// match the tier of the events it stands for, which the handler's tests check. A member not listed here is
/// treated as <see cref="IntakeRequestReadTier.Internal"/>, so a change added without a tier is withheld from
/// the request's own people rather than shown to them.
/// </remarks>
internal static class IntakeRequestNotificationTiers
{
    private static readonly IReadOnlyDictionary<NotificationEvent, IntakeRequestReadTier> Tiers =
        new Dictionary<NotificationEvent, IntakeRequestReadTier>
        {
            [NotificationEvent.StatusChanged] = IntakeRequestReadTier.Summary,
            [NotificationEvent.Withdrawn] = IntakeRequestReadTier.Summary,
            [NotificationEvent.Retitled] = IntakeRequestReadTier.Summary,
            [NotificationEvent.TimelineChanged] = IntakeRequestReadTier.Summary,
            [NotificationEvent.AssigneeChanged] = IntakeRequestReadTier.Summary,
            [NotificationEvent.Reparented] = IntakeRequestReadTier.Summary,
            [NotificationEvent.ChannelChanged] = IntakeRequestReadTier.Summary,
            [NotificationEvent.DetailsChanged] = IntakeRequestReadTier.Substance,
            [NotificationEvent.ParticipantsChanged] = IntakeRequestReadTier.Substance,
        };

    /// <summary>The tier a reader must hold to be told of the change.</summary>
    public static IntakeRequestReadTier TierOf(NotificationEvent notificationEvent) =>
        Tiers.GetValueOrDefault(notificationEvent, IntakeRequestReadTier.Internal);

    /// <summary>The changes a reader holding exactly <paramref name="tier"/> must hold it to be told of.</summary>
    public static NotificationEvent[] EventsIn(IntakeRequestReadTier tier) =>
        [.. Enum.GetValues<NotificationEvent>().Where(e => TierOf(e) == tier)];
}
