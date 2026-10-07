'use client'

import { CSSProperties, FC } from 'react'
import { MetricCard } from '../metrics'
import { SprintScopeDto } from '@/src/services/wayd-api'

export interface SprintSayDoMetricProps {
  scope: SprintScopeDto
  /** Measures by item count rather than the sprint's estimate. */
  byCount?: boolean
  cardStyle?: CSSProperties
}

/**
 * The share of a sprint's committed work the team completed: completed of
 * committed ÷ committed. Nothing when nothing was committed or the history is
 * incomplete, since then there is no ratio to read.
 */
const SprintSayDoMetric: FC<SprintSayDoMetricProps> = ({
  scope,
  byCount = false,
  cardStyle,
}) => {
  const ratio = byCount ? scope.totals.sayDoCount : scope.totals.sayDoEstimate
  if (scope.historyIncomplete || ratio === undefined || ratio === null)
    return null

  const completed = scope.totals.completedOfCommitted
  const committed = scope.totals.committed
  const value = byCount ? completed.count : completed.estimate
  const total = byCount ? committed.count : committed.estimate

  return (
    <MetricCard
      // Work still remaining means the sprint has not ended, so the ratio can still rise.
      title={scope.totals.remaining.count > 0 ? 'Say/Do so far' : 'Say/Do'}
      value={ratio * 100}
      precision={0}
      suffix="%"
      secondaryValue={`${value.toLocaleString()} of ${total.toLocaleString()} committed`}
      tooltip="Of the work in the sprint at its commitment point, the share completed by its end. Work added later does not count, and an item completed as Removed counts as completed."
      cardStyle={cardStyle}
    />
  )
}

export default SprintSayDoMetric
