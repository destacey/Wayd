'use client'

import { Button, Flex, Segmented, Typography } from 'antd'
import Link from 'next/link'
import { KeyboardEvent, useEffect, useRef } from 'react'
import NotificationList, {
  NoNotificationsMessage,
  NothingUnreadMessage,
} from '@/src/components/common/notifications/notification-list'
import {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
  useMarkAllNotificationsReadMutation,
} from '@/src/store/features/common/notifications-api'
import { useLocalStorageState } from '@/src/hooks'
import styles from './notification-bell.module.css'

const { Text } = Typography

/** How many entries the bell's panel shows; the page has the rest. */
const PanelPageSize = 10

type PanelView = 'all' | 'unread'

export interface NotificationPanelProps {
  /**
   * Closes the panel: after an entry or the page link is followed, or on
   * Escape, when focus goes back to the bell.
   */
  onClose: (options?: { returnFocus?: boolean }) => void
  /**
   * Shown in a drawer, which names itself and manages focus; otherwise the
   * panel is its own dialog and takes focus when it opens.
   */
  inDrawer?: boolean
}

/**
 * The newest notifications, as the bell opens them. Mounted only while open,
 * so each opening refetches rather than showing an entry the reader may no
 * longer be allowed to see.
 */
const NotificationPanel = ({ onClose, inDrawer }: NotificationPanelProps) => {
  const panelRef = useRef<HTMLDivElement>(null)
  // Remembered, since the panel unmounts on close and would otherwise reopen on All every time.
  const [view, setView] = useLocalStorageState<PanelView>(
    'wayd.notifications.panelView',
    'all',
  )
  const list = useGetMyNotificationsInfiniteQuery(
    { unreadOnly: view === 'unread' || undefined, pageSize: PanelPageSize },
    { refetchOnMountOrArgChange: true },
  )
  // Refreshed on opening, so the badge agrees with the entries below it.
  const { data: unread } = useGetUnreadNotificationCountQuery(undefined, {
    refetchOnMountOrArgChange: true,
  })
  const [markAllRead, markAll] = useMarkAllNotificationsReadMutation()

  const notifications = list.data?.pages[0]?.items ?? []

  useEffect(() => {
    if (!inDrawer) panelRef.current?.focus({ preventScroll: true })
  }, [inDrawer])

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !inDrawer) {
      event.stopPropagation()
      onClose({ returnFocus: true })
    }
  }

  return (
    <div
      ref={panelRef}
      className={`${styles.panel} ${inDrawer ? styles.panelInDrawer : ''}`}
      role={inDrawer ? undefined : 'dialog'}
      aria-label={inDrawer ? undefined : 'Notifications'}
      tabIndex={inDrawer ? undefined : -1}
      onKeyDown={onKeyDown}
    >
      <Flex
        className={styles.panelHeader}
        align="center"
        justify={inDrawer ? 'flex-end' : 'space-between'}
        gap="small"
        wrap
      >
        {inDrawer ? null : <Text strong>Notifications</Text>}
        <Flex align="center" gap="small">
          <Segmented<PanelView>
            size="small"
            value={view}
            onChange={setView}
            options={[
              { label: 'All', value: 'all' },
              { label: 'Unread', value: 'unread' },
            ]}
          />
          <Button
            size="small"
            type="link"
            disabled={!unread?.count}
            loading={markAll.isLoading}
            onClick={() => markAllRead()}
          >
            Mark all read
          </Button>
        </Flex>
      </Flex>
      <div className={styles.panelBody}>
        <NotificationList
          notifications={notifications}
          isLoading={list.isLoading}
          isError={list.isError}
          emptyMessage={
            view === 'unread' ? NothingUnreadMessage : NoNotificationsMessage
          }
          onNavigate={() => onClose()}
        />
      </div>
      <div className={styles.panelFooter}>
        <Link href="/account/notifications" onClick={() => onClose()}>
          See all notifications
        </Link>
      </div>
    </div>
  )
}

export default NotificationPanel
