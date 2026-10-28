'use client'

import { CheckOutlined, MutedOutlined } from '@ant-design/icons'
import { Alert, Button, Spin, Tag, Tooltip } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import { recordHref } from '../record/record-href'
import { recordId, typeLabel } from '../record/record-identity'
import { useMessage } from '@/src/components/contexts/messaging'
import { formatTimeAgo } from '@/src/utils/time-ago'
import { toApiError } from '@/src/utils/problem-details'
import { NotificationDto } from '@/src/services/wayd-api'
import { useMarkRecordNotificationsReadMutation } from '@/src/store/features/common/notifications-api'
import {
  useMuteRecordMutation,
  useUnmuteRecordMutation,
} from '@/src/store/features/common/notification-mutes-api'
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
  'No notifications yet. Wayd tells you here when something changes on a project, program or PI objective you follow or work on. Open one and select the eye at the top of its page to follow it.'

/** What an empty list says when only unread entries are shown. */
export const NothingUnreadMessage = 'You have read everything.'

interface NotificationEntryProps {
  notification: NotificationDto
  onOpen: (notification: NotificationDto) => void
  onMarkRead: (notification: NotificationDto) => void
  onMute: (notification: NotificationDto) => void
  /** Whether a mute of this entry's record is on its way. */
  isMuting: boolean
}

const NotificationEntry = ({
  notification,
  onOpen,
  onMarkRead,
  onMute,
  isMuting,
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
              className={styles.entryAction}
              icon={<CheckOutlined />}
              aria-label={`Mark ${record.name} read`}
              onClick={() => onMarkRead(notification)}
            />
          </Tooltip>
        ) : null}
        <Tooltip title="Mute: stop telling me about this">
          <Button
            type="text"
            size="small"
            className={styles.entryAction}
            icon={<MutedOutlined />}
            aria-label={`Mute ${record.name}`}
            loading={isMuting}
            onClick={() => onMute(notification)}
          />
        </Tooltip>
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
  const [muteRecord, muting] = useMuteRecordMutation()
  const [unmuteRecord] = useUnmuteRecordMutation()
  const messageApi = useMessage()

  const isMuting = (notification: NotificationDto) =>
    muting.isLoading &&
    muting.originalArgs?.ownerType === notification.record.ownerType &&
    muting.originalArgs?.ownerId === notification.record.id

  const mute = async (notification: NotificationDto) => {
    const { record } = notification
    const target = { ownerType: record.ownerType, ownerId: record.id }
    // Focus goes to the confirmation's Undo; it comes back here afterwards.
    const trigger =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null
    const response = await muteRecord(target)
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ?? `Could not mute ${record.name}.`,
      )
      return
    }

    // An undo must never unmute a mute this click did not make.
    if (response.data.wasAlreadyMuted) {
      messageApi.info(`${record.name} is already muted.`)
      return
    }

    const key = `mute:${recordId(record)}`
    const close = () => {
      messageApi.destroy(key)
      if (trigger?.isConnected) trigger.focus()
    }
    const undo = async () => {
      close()
      const undone = await unmuteRecord(target)
      if ('error' in undone) {
        messageApi.error(
          toApiError(undone.error).detail ?? `Could not unmute ${record.name}.`,
        )
      }
    }

    // Stays until dismissed, with focus on Undo, so keyboard and screen
    // reader users can reach it before it goes.
    messageApi.success({
      key,
      duration: 0,
      content: (
        <span>
          Muted {record.name}. You will not be told about it again.{' '}
          <Button type="link" size="small" autoFocus onClick={undo}>
            Undo
          </Button>
          <Button type="link" size="small" onClick={close}>
            Dismiss
          </Button>
        </span>
      ),
    })
  }

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
            onMute={mute}
            isMuting={isMuting(notification)}
          />
        ))}
      </ul>
      {footer}
    </>
  )
}

export default NotificationList
