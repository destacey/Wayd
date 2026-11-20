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
      return 'The successor has no planned dates, or the predecessor is planned to finish on or before the day the successor is planned to start.'
    case DependencyHealth.AtRisk:
      return 'The predecessor and successor are planned for the same time, or the predecessor has started without planned dates while the successor has them. Sprints that have ended give no planned dates.'
    case DependencyHealth.Unhealthy:
      return 'The predecessor is planned to finish after the successor, or has neither started nor planned dates while the successor has them; the successor has started, finished or been removed while the predecessor is still open; or the predecessor was removed while the successor is still open.'
    case DependencyHealth.Closed:
      return 'The predecessor is done, or it was removed and the successor is done or removed too. Planning no longer applies.'
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
