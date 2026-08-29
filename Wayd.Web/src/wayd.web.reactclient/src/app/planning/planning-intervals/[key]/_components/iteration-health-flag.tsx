'use client'

import { useGetPlanningIntervalIterationMetricsQuery } from '@/src/store/features/planning/planning-interval-api'
import {
  calculateIterationHealth,
  IterationHealthStatus,
} from '@/src/utils/iteration-health'
import { FlagFilled } from '@ant-design/icons'
import WaydTooltip from '@/src/components/common/wayd-tooltip'

interface IterationHealthFlagProps {
  piKey: number
  iterationKey: number
  start: Date
  end: Date
}

const statusToColorVar = (status: IterationHealthStatus): string | undefined => {
  switch (status) {
    case IterationHealthStatus.OnTrack:
    case IterationHealthStatus.Completed:
      return 'var(--ant-color-success)'
    case IterationHealthStatus.AtRisk:
      return 'var(--ant-color-warning)'
    case IterationHealthStatus.OffTrack:
      return 'var(--ant-color-error)'
    default:
      return undefined
  }
}

const IterationHealthFlag = ({
  piKey,
  iterationKey,
  start,
  end,
}: IterationHealthFlagProps) => {
  const { data: metrics } = useGetPlanningIntervalIterationMetricsQuery({
    planningIntervalKey: piKey,
    iterationKey,
  })

  if (!metrics) return null

  const { status } = calculateIterationHealth({
    startDate: start,
    endDate: end,
    total: metrics.totalWorkItems,
    completed: metrics.completedWorkItems,
  })

  const color = statusToColorVar(status)
  if (!color) return null

  return (
    <WaydTooltip title={`Health: ${status}`}>
      <FlagFilled
        style={{ color, fontSize: 14 }}
        aria-label={`Iteration health: ${status}`}
      />
    </WaydTooltip>
  )
}

export default IterationHealthFlag
