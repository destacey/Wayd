'use client'

import NotificationList from '@/src/components/common/notifications/notification-list'
import {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
} from '@/src/store/features/common/notifications-api'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import WidgetCard, { WidgetEmpty } from '../widget-card'
import { MAX_ROWS, rowsToShow } from '../widget-settings'
import { HomeWidgetProps } from '../widget-registry'

/** How many of the latest changes the widget shows; the rest are a click away. */
export const WHATS_CHANGED_COUNT = 5

const NOTIFICATIONS_HREF = '/account/notifications'

/** The latest changes to the records the caller follows or works on. */
const WhatsChangedWidget: FC<HomeWidgetProps> = ({ title, settings }) => {
  const rows = rowsToShow(settings, WHATS_CHANGED_COUNT)
  const { data, isLoading, isError } = useGetMyNotificationsInfiniteQuery({
    pageSize: MAX_ROWS,
  })
  const notifications = (data?.pages[0]?.items ?? []).slice(0, rows)

  return (
    <WidgetCard
      title={title}
      viewAllHref={NOTIFICATIONS_HREF}
      loading={isLoading}
      failed={isError}
      empty={notifications.length === 0}
      emptySummary="Nothing has changed"
    >
      {!isError && notifications.length === 0 ? (
        <WidgetEmpty message="Nothing has changed yet. Follow a project, program or PI objective, or take a role on one, and its changes appear here." />
      ) : (
        <NotificationList
          notifications={notifications}
          isLoading={false}
          isError={isError}
          emptyMessage=""
        />
      )}
    </WidgetCard>
  )
}

/** Records with notifications the caller has not read — the bell's count. */
export const UnreadNotificationsTile: FC = () => {
  const { data, isLoading, isError } = useGetUnreadNotificationCountQuery()
  return (
    <AttentionTile
      title="Unread notifications"
      value={isError ? undefined : (data?.count ?? 0)}
      capped={data?.isCapped}
      tooltip="Records you follow or work on with changes you have not read yet."
      href={NOTIFICATIONS_HREF}
      alert="warning"
      loading={isLoading}
    />
  )
}

export default WhatsChangedWidget
