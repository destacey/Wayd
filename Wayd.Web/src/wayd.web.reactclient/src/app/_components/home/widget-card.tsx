'use client'

import ViewAllLink from '@/src/components/common/view-all-link'
import { SettingOutlined } from '@ant-design/icons'
import { Button, Card, Popover, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { FC, ReactNode, useContext, useState } from 'react'
import styles from './home.module.css'
import { useWidgetState, WidgetState } from './widget-display'
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
  /** The widget's data failed to load; it then shows in full whatever its empty setting. */
  failed?: boolean
  /** The widget has nothing to show; its empty setting decides how it draws. */
  empty?: boolean
  /** The few words the one-line form says beside the title when empty, such as "No open risks". */
  emptySummary?: string
  children?: ReactNode
}

const stateOf = ({
  loading,
  failed,
  empty,
}: Pick<WidgetCardProps, 'loading' | 'failed' | 'empty'>): WidgetState =>
  loading ? 'loading' : failed ? 'failed' : empty ? 'empty' : 'rows'

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
 * It reports the widget's state to the page and, when the widget is empty,
 * draws it as its empty setting says: in full, as one line, or not at all.
 */
const WidgetCard: FC<WidgetCardProps> = ({
  title,
  viewAllHref,
  loading = false,
  flush = false,
  failed = false,
  empty = false,
  emptySummary = 'Nothing to show',
  children,
}) => {
  const settings = useContext(WidgetSettingsContext)
  const hasSettings = (settings?.widget.settings?.length ?? 0) > 0
  const form = useWidgetState(stateOf({ loading, failed, empty }))
  if (form === 'hidden') return null
  const isLine = form === 'line'
  return (
    <Card
      size="small"
      title={title}
      extra={
        viewAllHref || hasSettings || isLine ? (
          <span className={styles.widgetCardExtra}>
            {isLine ? <Text type="secondary">{emptySummary}</Text> : null}
            {/* Before the link, so "View all" ends at the same edge on every card. */}
            {settings && hasSettings ? (
              <WidgetSettingsButton {...settings} />
            ) : null}
            {viewAllHref ? <ViewAllLink href={viewAllHref} /> : null}
          </span>
        ) : undefined
      }
      className={`${styles.widgetCard} ${flush && !loading ? styles.flushCard : ''} ${isLine ? styles.lineCard : ''}`}
    >
      {isLine ? null : loading ? (
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
