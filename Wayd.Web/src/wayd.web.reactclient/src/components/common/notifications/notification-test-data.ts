import {
  EventActorKind,
  NotificationDto,
  NotificationEvent,
  NotificationReason,
  NotifiedRecordDto,
} from '@/src/services/wayd-api'

/** A project to be told about, overridable field by field. */
export const buildNotifiedRecord = (
  overrides: Partial<NotifiedRecordDto> = {},
): NotifiedRecordDto => ({
  ownerType: 'ppm.project',
  id: 'p-1',
  key: 'APOLLO',
  name: 'Apollo',
  statusName: 'Active',
  ...overrides,
})

/**
 * One unread entry: Ada Lovelace changed the status of a followed project a
 * minute ago. Tests override only what they are about.
 */
export const buildNotification = (
  overrides: Partial<NotificationDto> = {},
): NotificationDto => ({
  record: buildNotifiedRecord(),
  latestNotificationId: 9,
  event: NotificationEvent.StatusChanged,
  eventId: 'e-1',
  occurredOn: new Date(Date.now() - 60_000),
  actorKind: EventActorKind.User,
  actor: { id: 'emp-1', key: 1, name: 'Ada Lovelace' },
  reasons: [NotificationReason.Following],
  unreadCount: 1,
  count: 1,
  isUnread: true,
  ...overrides,
})

/** An entry with nothing left unread. */
export const buildReadNotification = (
  overrides: Partial<NotificationDto> = {},
): NotificationDto =>
  buildNotification({ isUnread: false, unreadCount: 0, ...overrides })
