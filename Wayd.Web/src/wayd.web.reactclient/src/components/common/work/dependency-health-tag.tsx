'use client'

import { Tag } from 'antd'
import { DependencyHealth } from '../../types'
import { FC, memo } from 'react'
import DependencyHealthTooltip from './dependency-health-tooltip'

export interface DependencyHealthTagProps {
  name: string
  health: DependencyHealth
}

/**
 * The antd preset for a dependency health, shared with the charts that plot it
 * so a slice and a tag for the same health are never different colors.
 */
export const getDependencyHealthTagColor = (
  health: DependencyHealth,
): string => {
  switch (health) {
    case DependencyHealth.Healthy:
      return 'success'
    case DependencyHealth.AtRisk:
      return 'warning'
    case DependencyHealth.Unhealthy:
      return 'error'
    default:
      return 'default'
  }
}

const getTagColor = getDependencyHealthTagColor

const DependencyHealthTag: FC<DependencyHealthTagProps> = ({
  name,
  health,
}) => {
  const color = getTagColor(health)
  return (
    <DependencyHealthTooltip health={health}>
      <Tag color={color}>{name}</Tag>
    </DependencyHealthTooltip>
  )
}

export default memo(DependencyHealthTag)
