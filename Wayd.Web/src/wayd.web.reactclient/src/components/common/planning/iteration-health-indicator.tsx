'use client'

import {
  calculateIterationHealth,
  IterationHealthStatus,
} from '@/src/utils/iteration-health'
import { CalendarDate } from '@/src/utils/calendar-date'
import { Badge } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { FC } from 'react'
import { PresetStatusColorType } from 'antd/es/_util/colors'

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
  showLabel = true,
}) => {
  const healthResult = calculateIterationHealth({
    startDate,
    endDate,
    total,
    completed,
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
    <WaydTooltip title={healthTooltip}>
      <span>
        <Badge status={color} text={showLabel ? healthResult.status : ''} />
      </span>
    </WaydTooltip>
  )
}

export default IterationHealthIndicator
