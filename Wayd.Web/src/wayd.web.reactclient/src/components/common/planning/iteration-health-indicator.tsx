'use client'

import {
  calculateSprintHealth,
  IterationHealthStatus,
  SprintCommitment,
} from '@/src/utils/iteration-health'
import { CalendarDate } from '@/src/utils/calendar-date'
import { Badge } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { FC } from 'react'
import { PresetStatusColorType } from 'antd/es/_util/colors'

const commitmentHealthTooltip = (
  <div>
    Health compares the work completed, as a share of what the team committed
    to, with the share the ideal line on the burn-down expects done by now. The
    line falls only on the team&apos;s working days, so weekends, holidays and
    team days off expect no progress:
    <br />• On Track: Within 10% of the ideal line
    <br />• At Risk: 10-25% behind
    <br />• Off Track: More than 25% behind
  </div>
)

const healthTooltip = (
  <div>
    Health is calculated by comparing actual progress against ideal linear
    burndown:
    <br />• On Track: Within 10% of ideal
    <br />• At Risk: 10-25% behind ideal
    <br />• Off Track: More than 25% behind
  </div>
)

export interface IterationHealthIndicatorProps {
  /** Start date of the iteration */
  startDate: CalendarDate
  /** End date of the iteration */
  endDate: CalendarDate
  /** Total planned points/items */
  total: number
  /** Completed points/items */
  completed: number
  /**
   * The sprint's commitment, and its burn-down's ideal line. When given,
   * health is measured against it — completed as a share of committed, against
   * the share the ideal line expects done by now — so work
   * added or re-estimated later doesn't count against the team. Without it,
   * health is measured against `total` over the iteration's days.
   */
  commitment?: SprintCommitment
  /** Show the label text (default: true) */
  showLabel?: boolean
}

/**
 * Displays an iteration health status indicator with a colored dot and label.
 * Calculates health based on burndown progress against ideal linear burndown.
 * Can be used for sprints, PI iterations, or any time-boxed iteration.
 *
 * @example
 * <IterationHealthIndicator
 *   startDate={sprint.start}
 *   endDate={sprint.end}
 *   total={totalPoints}
 *   completed={completedPoints}
 * />
 */
const IterationHealthIndicator: FC<IterationHealthIndicatorProps> = ({
  startDate,
  endDate,
  total,
  completed,
  commitment,
  showLabel = true,
}) => {
  const byCommitment = !!commitment && commitment.committed > 0
  const healthResult = calculateSprintHealth({
    startDate,
    endDate,
    total,
    completed,
    commitment,
  })

  const getHealthColor = (
    status: IterationHealthStatus,
  ): PresetStatusColorType => {
    switch (status) {
      case IterationHealthStatus.OnTrack:
      case IterationHealthStatus.Completed:
        return 'success'
      case IterationHealthStatus.AtRisk:
        return 'warning'
      case IterationHealthStatus.OffTrack:
        return 'error'
      case IterationHealthStatus.NotStarted:
      default:
        return 'default'
    }
  }

  const color = getHealthColor(healthResult.status)

  // span is needed for Tooltip to work with Badge
  return (
    <WaydTooltip title={byCommitment ? commitmentHealthTooltip : healthTooltip}>
      <span>
        <Badge status={color} text={showLabel ? healthResult.status : ''} />
      </span>
    </WaydTooltip>
  )
}

export default IterationHealthIndicator
