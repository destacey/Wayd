'use client'

import ViewAllLink from '@/src/components/common/view-all-link'
import { SettingOutlined } from '@ant-design/icons'
import { Button, Card, Popover, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { FC, ReactNode, useContext, useState } from 'react'
import styles from './home.module.css'
import WidgetSettingsForm, {
  WidgetSettingsAccess,
  WidgetSettingsContext,
} from './widget-settings-form'

const { Text } = Typography

export interface WidgetCardProps {
  title: string
  /** Where "View all" goes: the full list the widget shows the top of. */
  viewAllHref?: string
  /** Draws a skeleton in place of the body while the widget's data loads. */
  loading?: boolean
  /**
   * Drops the body's padding, for rows that carry their own and run edge to
   * edge. Only while there are rows: a skeleton or a one-line state needs it.
   */
  flush?: boolean
  children?: ReactNode
}

/** The gear that opens a widget's settings from its card. */
const WidgetSettingsButton: FC<WidgetSettingsAccess> = (access) => {
  const [open, setOpen] = useState(false)
  const label = `${access.widget.title} settings`
  return (
    <Popover
      trigger="click"
      placement="bottomRight"
      open={open}
      onOpenChange={setOpen}
      // Beside the gear rather than at the end of the body, so Tab moves from
      // the gear into the form and on to the card.
      getPopupContainer={(trigger) => trigger.parentElement ?? document.body}
      title={label}
      content={<WidgetSettingsForm {...access} />}
    >
      <Button
        type="text"
        size="small"
        icon={<SettingOutlined />}
        aria-label={label}
        className={`${styles.widgetSettingsButton} ${open ? styles.widgetSettingsButtonOpen : ''}`}
      />
    </Popover>
  )
}

/**
 * The frame every home widget sits in: a titled card, with a link to the full
 * list and, for a widget that declares settings, the gear that changes them.
 */
const WidgetCard: FC<WidgetCardProps> = ({
  title,
  viewAllHref,
  loading = false,
  flush = false,
  children,
}) => {
  const settings = useContext(WidgetSettingsContext)
  const hasSettings = (settings?.widget.settings?.length ?? 0) > 0
  return (
    <Card
      size="small"
      title={title}
      extra={
        viewAllHref || hasSettings ? (
          <span className={styles.widgetCardExtra}>
            {/* Before the link, so "View all" ends at the same edge on every card. */}
            {settings && hasSettings ? (
              <WidgetSettingsButton {...settings} />
            ) : null}
            {viewAllHref ? <ViewAllLink href={viewAllHref} /> : null}
          </span>
        ) : undefined
      }
      className={`${styles.widgetCard} ${flush && !loading ? styles.flushCard : ''}`}
    >
      {loading ? (
        <Skeleton active title={false} paragraph={{ rows: 3 }} />
      ) : (
        children
      )}
    </Card>
  )
}

export interface WidgetEmptyProps {
  /** One line saying how the widget fills, never just "No data". */
  message: string
  /** Where to go to make it fill, when there is somewhere. */
  link?: { href: string; label: string }
}

/** What a widget that applies but has nothing to show says, in place of vanishing. */
export const WidgetEmpty: FC<WidgetEmptyProps> = ({ message, link }) => (
  <Text type="secondary" className={styles.widgetEmpty}>
    {message}
    {link ? (
      <>
        {' '}
        <Link href={link.href}>{link.label}</Link>
      </>
    ) : null}
  </Text>
)

/** What a widget says when its data failed to load, so a failure never reads as "nothing to show". */
export const WidgetError: FC<{ message: string }> = ({ message }) => (
  <Text type="danger" className={styles.widgetEmpty}>
    {message}
  </Text>
)

export default WidgetCard
