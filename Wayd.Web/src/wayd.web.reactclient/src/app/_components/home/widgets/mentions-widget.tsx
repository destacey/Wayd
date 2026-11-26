'use client'

import NotificationList from '@/src/components/common/notifications/notification-list'
import { recordDiscussionHref } from '@/src/components/common/record/record-href'
import {
  NotificationDto,
  NotificationEvent,
  NotificationReason,
} from '@/src/services/api/wayd.schemas'
import {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
} from '@/src/store/features/common/notifications-api'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import WidgetCard, { WidgetEmpty } from '../widget-card'
import { MAX_ROWS, rowsToShow } from '../widget-settings'
import {
  HomeTileProps,
  HomeWidgetProps,
  widgetAnchorId,
} from '../widget-registry'

/** How many conversations the widget shows; the rest are a click away. */
export const MENTIONS_COUNT = 5

/** The widget's id, which its tile scrolls to. */
export const MENTIONS_WIDGET_ID = 'mentions'

/** The reasons that mean someone asked for the caller: a mention, or a reply in their thread. */
export const MentionReasons = [
  NotificationReason.Mentioned,
  NotificationReason.Thread,
]

const NOTIFICATIONS_HREF = '/account/notifications'

/**
 * Unread entries first, then the read ones, newest first within each. Each
 * request is newest first already; an entry unread on the first is left out
 * of the second, which lists every entry.
 */
export const unreadFirst = (
  unread: NotificationDto[],
  all: NotificationDto[],
): NotificationDto[] => {
  const key = (n: NotificationDto) => `${n.record.ownerType}:${n.record.id}`
  const shown = new Set(unread.map(key))
  return [...unread, ...all.filter((n) => !n.isUnread && !shown.has(key(n)))]
}

/** The events whose `eventId` is the id of the message that was posted. */
const messageEvents: NotificationEvent[] = [
  NotificationEvent.MessagePosted,
  NotificationEvent.InternalNotePosted,
]

/** Opens the message itself, or the record's conversation when the entry is about none. */
const conversationHref = (notification: NotificationDto) =>
  recordDiscussionHref(
    notification.record,
    messageEvents.includes(notification.event)
      ? notification.eventId
      : undefined,
  )

/** The conversations that asked for the caller: mentions and replies in their threads, unread first. */
const MentionsWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, MENTIONS_COUNT)
  const unread = useGetMyNotificationsInfiniteQuery({
    pageSize: MAX_ROWS,
    unreadOnly: true,
    reasons: MentionReasons,
  })
  const unreadItems = unread.data?.pages[0]?.items ?? []
  // Read entries are asked for only when the unread ones leave rows to fill.
  const skipRead =
    unread.isLoading || unread.isError || unreadItems.length >= rows
  const all = useGetMyNotificationsInfiniteQuery(
    { pageSize: MAX_ROWS, reasons: MentionReasons },
    { skip: skipRead },
  )

  const isLoading = unread.isLoading || (!skipRead && all.isLoading)
  const isError = unread.isError || (!skipRead && all.isError)
  const notifications = unreadFirst(
    unreadItems,
    skipRead ? [] : (all.data?.pages[0]?.items ?? []),
  ).slice(0, rows)

  return (
    <WidgetCard
      title={title}
      viewAllHref={NOTIFICATIONS_HREF}
      loading={isLoading}
      failed={isError}
      empty={notifications.length === 0}
      emptySummary="Nobody has asked for you"
    >
      {!isError && notifications.length === 0 ? (
        <WidgetEmpty message="Nobody has mentioned you or replied in your threads yet. When someone writes @ and your name in a record's discussion, or replies in a thread you are in, it appears here." />
      ) : (
        <NotificationList
          notifications={notifications}
          isLoading={false}
          isError={isError}
          emptyMessage=""
          entryHref={conversationHref}
        />
      )}
    </WidgetCard>
  )
}

/** Conversations with a mention or reply the caller has not read. */
export const UnreadMentionsTile: FC<HomeTileProps> = ({ title }) => {
  const { data, isLoading, isError } = useGetUnreadNotificationCountQuery({
    reasons: MentionReasons,
  })
  return (
    <AttentionTile
      title={title}
      value={isError ? undefined : (data?.count ?? 0)}
      capped={data?.isCapped}
      tooltip="Records where someone mentioned you or replied in your thread, and you have not read it yet."
      href={`#${widgetAnchorId(MENTIONS_WIDGET_ID)}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default MentionsWidget
