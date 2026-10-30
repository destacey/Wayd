import {
  EventActorKind,
  NotificationDto,
  NotificationEvent,
  NotificationReason,
  UnreadNotificationCountDto,
} from '@/src/services/wayd-api'

/**
 * What each change did, worded to sit between the actor and the record's name:
 * "Ada *changed the status of* Apollo". The server stores facts, not prose, so
 * the sentence is always built from the record's current name.
 */
const actions: Record<NotificationEvent, string> = {
  [NotificationEvent.Created]: 'created',
  [NotificationEvent.StatusChanged]: 'changed the status of',
  [NotificationEvent.LifecycleChanged]: 'changed the lifecycle of',
  [NotificationEvent.HealthReported]: 'reported health on',
  [NotificationEvent.TimelineChanged]: 'changed the dates of',
  [NotificationEvent.RolesChanged]: 'changed the people on',
  [NotificationEvent.Reparented]: 'moved',
  [NotificationEvent.ScoreRecorded]: 'scored',
  [NotificationEvent.Withdrawn]: 'withdrew',
  [NotificationEvent.Retitled]: 'renamed',
  [NotificationEvent.DetailsChanged]: 'changed the details of',
  [NotificationEvent.AssigneeChanged]: 'changed the assignee of',
  [NotificationEvent.ParticipantsChanged]: 'changed the participants of',
  [NotificationEvent.ChannelChanged]: 'changed the channel of',
  [NotificationEvent.MessagePosted]: 'posted on',
  [NotificationEvent.InternalNotePosted]: 'added an internal note to',
}

/** The words between the actor and the record for a change. */
export const notificationAction = (event: NotificationEvent): string =>
  actions[event] ?? 'updated'

/**
 * Who made the newest change. A person's account with no linked employee has
 * no name to show; anything else was the platform acting on its own.
 */
export const notificationActorName = (
  notification: Pick<NotificationDto, 'actor' | 'actorKind'>,
): string =>
  notification.actor?.name ??
  (notification.actorKind === EventActorKind.User ? 'Someone' : 'Wayd')

/**
 * The tail for an entry that collapses several changes, counting every change
 * after the newest whether read or not: " and 2 other updates". Empty for one.
 */
export const otherUpdatesText = (count: number): string => {
  const others = count - 1
  if (others < 1) return ''
  return ` and ${others} other ${others === 1 ? 'update' : 'updates'}`
}

/** Reasons phrased as "You … this", joinable into one clause. */
const youVerbs: Partial<Record<NotificationReason, string>> = {
  [NotificationReason.Following]: 'follow',
  [NotificationReason.Participant]: 'take part in',
  [NotificationReason.Role]: 'work on',
  [NotificationReason.Requester]: 'requested',
}

/** Reasons that need a sentence of their own. */
const otherReasons: Partial<Record<NotificationReason, string>> = {
  [NotificationReason.Assigned]: 'It is assigned to you',
  [NotificationReason.Mentioned]: 'You were mentioned',
  [NotificationReason.Desk]: 'It is waiting on your desk',
  [NotificationReason.Thread]: 'You are in the thread',
}

/** "follow", "follow and work on", "follow, work on and requested". */
const joinVerbs = (verbs: string[]) =>
  verbs.length > 1
    ? `${verbs.slice(0, -1).join(', ')} and ${verbs[verbs.length - 1]}`
    : verbs[0]

/**
 * Why the reader was told, GitHub's "you're receiving this because…": "You
 * follow and work on this". A reason the client has no words for is left
 * out rather than shown raw.
 */
export const notificationReasonText = (
  reasons: NotificationReason[],
): string => {
  const verbs = reasons.flatMap((r) => (youVerbs[r] ? [youVerbs[r]] : []))
  const sentences = [
    ...(verbs.length ? [`You ${joinVerbs(verbs)} this`] : []),
    ...reasons.flatMap((r) => (otherReasons[r] ? [otherReasons[r]] : [])),
  ]
  return sentences.join(' · ')
}

/** The unread count as the bell shows it: "99+" past the server's cap. */
export const unreadCountText = (
  unread: UnreadNotificationCountDto | undefined,
): string | undefined => {
  if (!unread || unread.count < 1) return undefined
  return unread.isCapped ? `${unread.count}+` : `${unread.count}`
}
