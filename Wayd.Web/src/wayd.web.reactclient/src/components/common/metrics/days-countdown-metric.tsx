'use client'

import { FC } from 'react'
import { daysRemaining, percentageElapsed } from '@/src/utils'
import { IterationState } from '../../types'
import { MetricCard } from '.'

export interface DaysCountdownMetricProps {
  /**
   * Current state of the item being tracked
   */
  state: IterationState

  /**
   * Start date (used when state is 'Future')
   */
  startDate: Date

  /**
   * End date (used when state is 'Active ')
   */
  endDate: Date

  /**
   * Optional custom labels for different states
   */
  labels?: {
    future?: string
    active?: string
  }

  /**
   * Optional style to apply to the card. Named to match the other metrics —
   * as `style` it was passed to a `MetricCard` prop that does not exist and
   * silently dropped, leaving this card sized differently from its row.
   */
  cardStyle?: React.CSSProperties
}

const DaysCountdownMetric: FC<DaysCountdownMetricProps> = ({
  state,
  startDate,
  endDate,
  labels = {},
  cardStyle,
}) => {
  const metric = (() => {
    switch (state) {
      case IterationState.Future: {
        const daysUntilStart = daysRemaining(startDate)
        return {
          title: labels.future ?? 'Days Until Start',
          value: daysUntilStart,
          secondaryValue: null, // No percentage for future iterations
        }
      }
      case IterationState.Active: {
        const daysLeft = daysRemaining(endDate)
        const percentage = percentageElapsed(startDate, endDate)
        return {
          title: labels.active ?? 'Days Remaining',
          value: daysLeft,
          secondaryValue: `${percentage.toFixed(0)}%`,
        }
      }
      default:
        return null
    }
  })()

  if (!metric) return null

  const suffix = metric.value === 1 ? 'day' : 'days'

  return (
    <MetricCard
      title={metric.title}
      value={metric.value}
      suffix={suffix}
      secondaryValue={metric.secondaryValue ?? undefined}
      cardStyle={cardStyle}
      tooltip={
        state === IterationState.Active
          ? 'Days remaining until the iteration ends. Percentage shows how much time has elapsed.'
          : 'Days until the iteration starts.'
      }
    />
  )
}

export default DaysCountdownMetric
