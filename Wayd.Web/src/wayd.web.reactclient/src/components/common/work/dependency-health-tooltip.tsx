'use client'

import { DependencyHealth } from '../../types'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { FC, memo, ReactNode } from 'react'

export interface DependencyHealthTooltipProps {
  health: DependencyHealth
  children: ReactNode
}

export const getHealthDescription = (health: DependencyHealth): string => {
  switch (health) {
    case DependencyHealth.Healthy:
      return 'The predecessor is done, planned with no successor plan, or is planned to complete on or before the successor.'
    case DependencyHealth.AtRisk:
      return 'Neither the predecessor nor successor have future planned dates. Sprints in the past are not considered for date comparisons.'
    case DependencyHealth.Unhealthy:
      return 'The predecessor was removed, is planned to complete after the successor needs it, or the successor is done or removed.'
    default:
      return 'The health status of this dependency has not been determined or reported.'
  }
}

const DependencyHealthTooltip: FC<DependencyHealthTooltipProps> = ({
  health,
  children,
}) => {
  const description = getHealthDescription(health)

  return <WaydTooltip title={description}>{children}</WaydTooltip>
}

export default memo(DependencyHealthTooltip)
