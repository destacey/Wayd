'use client'

import useTheme from '@/src/components/contexts/theme'
import {
  calculateIterationHealth,
  IterationHealthStatus,
} from '@/src/utils/iteration-health'
import { CalendarDate } from '@/src/utils/calendar-date'
import { Progress } from 'antd'
import { FC } from 'react'

export interface IterationProgressBarProps {
  /** Start date of the iteration */
  startDate: CalendarDate
  /** End date of the iteration */
  endDate: CalendarDate
  /** Total planned points/items */
  total: number
  /** Completed points/items */
  completed: number
  /** Size of the progress bar (default: 'small') */
  size?: 'small' | 'medium'
  /** Whether to show the percentage info (default: false) */
  showInfo?: boolean
}

/**
 * Displays an iteration progress bar with color based on health status.
 * Color is determined by comparing actual progress against ideal burndown:
 * - Green: On Track or Completed
 * - Yellow: At Risk
 * - Red: Off Track
 *
 * @example
 * <IterationProgressBar
 *   startDate={sprint.start}
 *   endDate={sprint.end}
 *   total={totalPoints}
 *   completed={completedPoints}
 * />
 */
const IterationProgressBar: FC<IterationProgressBarProps> = ({
  startDate,
  endDate,
  total,
  completed,
  size = 'small',
  showInfo = false,
}) => {
  const { token } = useTheme()

  const completionPercent = total > 0 ? (completed / total) * 100 : 0

  const healthResult = calculateIterationHealth({
    startDate,
    endDate,
    total,
    completed,
  })

  const getProgressBarColor = (): string => {
    switch (healthResult.status) {
      case IterationHealthStatus.OnTrack:
      case IterationHealthStatus.Completed:
        return token.colorSuccess
      case IterationHealthStatus.AtRisk:
        return token.colorWarning
      case IterationHealthStatus.OffTrack:
        return token.colorError
      default:
        return token.colorSuccess
    }
  }

  return (
    <Progress
      percent={completionPercent}
      showInfo={showInfo}
      strokeColor={getProgressBarColor()}
      size={size}
    />
  )
}

export default IterationProgressBar
