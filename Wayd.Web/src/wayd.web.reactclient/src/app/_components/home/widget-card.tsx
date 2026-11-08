'use client'

import ViewAllLink from '@/src/components/common/view-all-link'
import { Card, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { FC, ReactNode } from 'react'
import styles from './home.module.css'

const { Text } = Typography

export interface WidgetCardProps {
  title: string
  /** Where "View all" goes: the full list the widget shows the top of. */
  viewAllHref?: string
  /** How many the full list holds, shown as "View all 23" when the widget shows fewer. */
  viewAllCount?: number
  /** Draws a skeleton in place of the body while the widget's data loads. */
  loading?: boolean
  /**
   * Drops the body's padding, for rows that carry their own and run edge to
   * edge. Only while there are rows: a skeleton or a one-line state needs it.
   */
  flush?: boolean
  children?: ReactNode
}

/** The frame every home widget sits in: a titled card, with a link to the full list. */
const WidgetCard: FC<WidgetCardProps> = ({
  title,
  viewAllHref,
  viewAllCount,
  loading = false,
  flush = false,
  children,
}) => (
  <Card
    size="small"
    title={title}
    extra={
      viewAllHref ? (
        <ViewAllLink href={viewAllHref} count={viewAllCount} />
      ) : undefined
    }
    className={flush && !loading ? styles.flushCard : undefined}
  >
    {loading ? (
      <Skeleton active title={false} paragraph={{ rows: 3 }} />
    ) : (
      children
    )}
  </Card>
)

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
