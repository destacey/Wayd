'use client'

import { MetricCard } from '@/src/components/common/metrics'
import { Typography } from 'antd'
import { useRouter } from 'next/navigation'
import { FC } from 'react'

const { Text } = Typography

/** How a tile's number is coloured once it is above zero. */
export type AttentionAlert = 'error' | 'warning'

/**
 * The colour of a tile's number: the alert's colour above zero, otherwise the
 * default. A zero is good news and never reads as a warning.
 */
export const attentionColor = (
  value: number | undefined,
  alert: AttentionAlert,
): string | undefined =>
  value !== undefined && value > 0
    ? alert === 'error'
      ? 'var(--ant-color-error)'
      : 'var(--ant-color-warning)'
    : undefined

export interface AttentionTileProps {
  title: string
  /** The count, or undefined when it could not be loaded. */
  value: number | undefined
  tooltip: string
  /** The list behind the count: a route, or `#id` for a widget on this page. */
  href: string
  alert: AttentionAlert
  loading: boolean
  /** The real count is higher than `value`, which the server capped: shown as "99+". */
  capped?: boolean
  /**
   * Where the count comes from, under the number — "on your projects" — when
   * the title alone would not say. The strip mixes areas, so "Overdue tasks"
   * without it could be anyone's tasks.
   */
  context?: string
}

/** One count in the home page's attention strip, linking to the list behind it. */
const AttentionTile: FC<AttentionTileProps> = ({
  title,
  value,
  tooltip,
  href,
  alert,
  loading,
  capped = false,
  context,
}) => {
  const router = useRouter()
  const color = attentionColor(value, alert)

  const open = () => {
    if (href.startsWith('#')) {
      document
        .getElementById(href.slice(1))
        ?.scrollIntoView({ behavior: 'smooth', block: 'start' })
      return
    }
    router.push(href)
  }

  return (
    <MetricCard
      title={title}
      // A dash, not a zero, when the count failed: a zero would claim all is well.
      value={value ?? '—'}
      suffix={capped ? '+' : undefined}
      tooltip={tooltip}
      loading={loading}
      valueStyle={color ? { color } : undefined}
      secondaryValue={
        context ? <Text type="secondary">{context}</Text> : undefined
      }
      onClick={open}
      ariaLabel={
        loading
          ? title
          : `${title}${context ? ` ${context}` : ''}: ${value ?? 'unavailable'}${capped ? '+' : ''}`
      }
    />
  )
}

export default AttentionTile
