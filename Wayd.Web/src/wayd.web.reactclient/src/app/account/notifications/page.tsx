'use client'

import { Button, Flex, Segmented } from 'antd'
import { useState } from 'react'
import { PageTitle, WaydEmpty } from '@/src/components/common'
import NotificationList, {
  NoNotificationsMessage,
  NothingUnreadMessage,
} from '@/src/components/common/notifications/notification-list'
import useAuth from '@/src/components/contexts/auth'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  notificationEntries,
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
  useMarkAllNotificationsReadMutation,
} from '@/src/store/features/common/notifications-api'
import styles from './notifications.module.css'

/** How many entries each page of the list loads. */
const NotificationsPageSize = 25

type View = 'all' | 'unread'

const NotificationsContent = () => {
  const [view, setView] = useState<View>('all')
  const list = useGetMyNotificationsInfiniteQuery(
    {
      unreadOnly: view === 'unread' || undefined,
      pageSize: NotificationsPageSize,
    },
    // Never show an entry from an earlier visit: the reader may have lost
    // access to its record since.
    { refetchOnMountOrArgChange: true },
  )
  // Decides "Mark all read", which covers entries not loaded yet.
  const { data: unread } = useGetUnreadNotificationCountQuery(undefined, {
    refetchOnMountOrArgChange: true,
  })
  const [markAllRead, markAll] = useMarkAllNotificationsReadMutation()

  const notifications = notificationEntries(list.data?.pages)

  return (
    <>
      <Flex align="center" justify="space-between" gap="small" wrap>
        <Segmented<View>
          value={view}
          onChange={setView}
          options={[
            { label: 'All', value: 'all' },
            { label: 'Unread', value: 'unread' },
          ]}
        />
        <Button
          disabled={!unread?.count}
          loading={markAll.isLoading}
          onClick={() => markAllRead()}
        >
          Mark all read
        </Button>
      </Flex>
      <div className={styles.pane}>
        <NotificationList
          notifications={notifications}
          isLoading={list.isLoading}
          isError={list.isError}
          emptyMessage={
            view === 'unread' ? NothingUnreadMessage : NoNotificationsMessage
          }
          footer={
            list.hasNextPage ? (
              <div className={styles.more}>
                <Button
                  loading={list.isFetchingNextPage}
                  onClick={() => list.fetchNextPage()}
                >
                  Load more
                </Button>
              </div>
            ) : null
          }
        />
      </div>
    </>
  )
}

const NotificationsPage = () => {
  useDocumentTitle('Notifications')
  const { user } = useAuth()

  return (
    <div className={styles.page}>
      <PageTitle
        title="Notifications"
        subtitle="Changes to the records you follow or take part in, newest first. Opening one marks it read."
      />
      {user?.employeeId ? (
        <NotificationsContent />
      ) : (
        <WaydEmpty message="Notifications need your account to be linked to an employee. Ask an administrator to link it in Settings → Users." />
      )}
    </div>
  )
}

export default requireFeatureFlag(NotificationsPage, 'following')
