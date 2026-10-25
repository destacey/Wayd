'use client'

import { CheckOutlined } from '@ant-design/icons'
import { Alert, Button, Spin, Tag, Tooltip } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import { recordHref } from '../record/record-href'
import { recordId, typeLabel } from '../record/record-identity'
import { formatTimeAgo } from '@/src/utils/time-ago'
import { NotificationDto } from '@/src/services/wayd-api'
import { useMarkRecordNotificationsReadMutation } from '@/src/store/features/common/notifications-api'
import WaydEmpty from '../wayd-empty'
import {
  notificationAction,
  notificationActorName,
  notificationReasonText,
  otherUpdatesText,
} from './notification-wording'
import styles from './notifications.module.css'

/** What an empty list says when nothing has ever arrived: how to get told things. */
export const NoNotificationsMessage =
  'No notifications yet. Wayd tells you here when something changes on a project, program or PI objective you follow or take part in. Open one and select the eye at the top of its page to follow it.'

/** What an empty list says when only unread entries are shown. */
export const NothingUnreadMessage = 'You have read everything.'

interface NotificationEntryProps {
  notification: NotificationDto
  onOpen: (notification: NotificationDto) => void
  onMarkRead: (notification: NotificationDto) => void
}

const NotificationEntry = ({
  notification,
  onOpen,
  onMarkRead,
}: NotificationEntryProps) => {
  const { record } = notification
  const href = recordHref(record)
  const reason = notificationReasonText(notification.reasons)
  const occurredOn = new Date(notification.occurredOn)

  const body = (
    <span className={styles.body}>
      <span className={styles.sentence}>
        {notification.isUnread ? (
          <span className={styles.visuallyHidden}>Unread: </span>
        ) : null}
        <strong>{notificationActorName(notification)}</strong>{' '}
        {notificationAction(notification.event)} <strong>{record.name}</strong>
        {otherUpdatesText(notification.count)}
      </span>
      <span className={styles.meta}>
        {typeLabel(record.ownerType)} {record.key}
        {record.parentName ? ` · ${record.parentName}` : ''}
        {' · '}
        {Number.isNaN(occurredOn.getTime()) ? null : (
          <time dateTime={occurredOn.toISOString()}>
            {formatTimeAgo(occurredOn)}
          </time>
        )}
      </span>
      {reason ? <span className={styles.reason}>{reason}</span> : null}
    </span>
  )

  return (
    <li
      className={`${styles.entry} ${notification.isUnread ? styles.entryUnread : ''}`}
    >
      <span className={styles.dot} aria-hidden />
      {href ? (
        <Link
          href={href}
          className={styles.link}
          onClick={() => onOpen(notification)}
        >
          {body}
        </Link>
      ) : (
        body
      )}
      <span className={styles.trail}>
        {notification.unreadCount > 1 ? (
          <Tooltip title={`${notification.unreadCount} unread`}>
            <span className={styles.unreadCount}>
              {notification.unreadCount}
              <span className={styles.visuallyHidden}> unread</span>
            </span>
          </Tooltip>
        ) : null}
        <Tag>{record.statusName}</Tag>
        {notification.isUnread ? (
          <Tooltip title="Mark read">
            <Button
              type="text"
              size="small"
              className={styles.markRead}
              icon={<CheckOutlined />}
              aria-label={`Mark ${record.name} read`}
              onClick={() => onMarkRead(notification)}
            />
          </Tooltip>
        ) : null}
      </span>
    </li>
  )
}

export interface NotificationListProps {
  notifications: NotificationDto[]
  isLoading: boolean
  isError: boolean
  /** Shown when the list is empty. */
  emptyMessage: string
  /** Called after an entry's link is followed, such as to close a panel. */
  onNavigate?: () => void
  /** Rendered after the entries, such as a "load more" button. */
  footer?: ReactNode
}

/**
 * The reader's notifications, one entry per record as the server grouped
 * them. Opening an entry marks that record's notifications read and goes to
 * the record; viewing the list marks nothing.
 */
const NotificationList = ({
  notifications,
  isLoading,
  isError,
  emptyMessage,
  onNavigate,
  footer,
}: NotificationListProps) => {
  const [markRecordRead] = useMarkRecordNotificationsReadMutation()

  const markRead = (notification: NotificationDto) => {
    // Navigation never waits on this, and a failure leaves the entry unread to try again.
    markRecordRead({
      ownerType: notification.record.ownerType,
      ownerId: notification.record.id,
    })
  }

  const onOpen = (notification: NotificationDto) => {
    if (notification.isUnread) markRead(notification)
    onNavigate?.()
  }

  if (isError) {
    return (
      <div className={styles.state}>
        <Alert type="error" title="Could not load your notifications." />
      </div>
    )
  }

  if (isLoading) {
    return (
      <div className={styles.state}>
        <Spin />
      </div>
    )
  }

  if (notifications.length === 0) {
    return (
      <div className={styles.state}>
        <WaydEmpty message={emptyMessage} />
      </div>
    )
  }

  return (
    <>
      {/* role="list" keeps list semantics in Safari, which drops them under list-style: none. */}
      <ul className={styles.list} aria-label="Notifications" role="list">
        {notifications.map((notification) => (
          <NotificationEntry
            key={recordId(notification.record)}
            notification={notification}
            onOpen={onOpen}
            onMarkRead={markRead}
          />
        ))}
      </ul>
      {footer}
    </>
  )
}

export default NotificationList
