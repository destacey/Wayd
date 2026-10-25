'use client'

import { BellOutlined } from '@ant-design/icons'
import { Badge, Button, Drawer, Grid, Popover, Tooltip } from 'antd'
import { useRef, useState } from 'react'
import useAuth from '@/src/components/contexts/auth'
import useTheme from '@/src/components/contexts/theme'
import { unreadCountText } from '@/src/components/common/notifications/notification-wording'
import { useFeatureFlag } from '@/src/hooks'
import {
  UnreadCountPollingInterval,
  useGetUnreadNotificationCountQuery,
} from '@/src/store/features/common/notifications-api'
import NotificationPanel from './notification-panel'

const { useBreakpoint } = Grid

/**
 * The bell in the header: the unread count, and the newest notifications in a
 * popover (a drawer on a phone). Absent when following is off or the account
 * has no linked employee, since nobody else can be told anything.
 */
const NotificationBell = () => {
  const { user } = useAuth()
  const { isEnabled } = useFeatureFlag('following')
  const enabled = isEnabled && !!user?.employeeId

  if (!enabled) return null
  return <NotificationBellButton />
}

const NotificationBellButton = () => {
  const { appBar } = useTheme()
  const screens = useBreakpoint()
  const isPhone = !screens.md
  const [open, setOpen] = useState(false)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const { data: unread } = useGetUnreadNotificationCountQuery(undefined, {
    pollingInterval: UnreadCountPollingInterval,
    skipPollingIfUnfocused: true,
    refetchOnFocus: true,
  })

  const countText = unreadCountText(unread)
  const label = countText
    ? `Notifications, ${countText} unread`
    : 'Notifications'
  const close = ({ returnFocus = false } = {}) => {
    setOpen(false)
    if (returnFocus) triggerRef.current?.focus()
  }

  // One stable trigger element: a replaced node makes the popover flicker.
  const trigger = (
    <Button
      ref={triggerRef}
      type="text"
      aria-label={label}
      aria-haspopup="dialog"
      aria-expanded={open}
      onClick={isPhone ? () => setOpen(true) : undefined}
      style={{ color: appBar.color }}
      icon={
        <Badge count={countText} size="small" offset={[0, -2]}>
          <BellOutlined />
        </Badge>
      }
    />
  )

  if (isPhone) {
    return (
      <>
        {trigger}
        <Drawer
          title="Notifications"
          placement="right"
          size="100%"
          open={open}
          onClose={() => close()}
          closable
          destroyOnHidden
          styles={{ body: { padding: 0 } }}
        >
          <NotificationPanel onClose={close} inDrawer />
        </Drawer>
      </>
    )
  }

  return (
    <Popover
      trigger="click"
      placement="bottomRight"
      open={open}
      onOpenChange={setOpen}
      destroyOnHidden
      arrow={false}
      styles={{ container: { padding: 0 } }}
      content={<NotificationPanel onClose={close} />}
    >
      <Tooltip title={open ? undefined : 'Notifications'}>{trigger}</Tooltip>
    </Popover>
  )
}

export default NotificationBell
