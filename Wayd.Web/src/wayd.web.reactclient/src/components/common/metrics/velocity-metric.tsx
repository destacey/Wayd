'use client'

import { CSSProperties, FC } from 'react'
import { MetricCard } from '.'
import useTheme from '@/src/components/contexts/theme'
import { SizingMethod } from '@/src/services/wayd-api'
import { isSizingMethod, sizingMethodMeasure } from '@/src/utils'

const getTooltipText = (sizingMethod: SizingMethod): string =>
  `Work in the sprint that is completed (Done or Removed), measured in ${sizingMethodMeasure(sizingMethod)}. Percentage shown represents the portion of total sprint work that is complete.`

export interface VelocityMetricProps {
  completed: number
  total: number
  tooltip?: string | SizingMethod
  cardStyle?: CSSProperties
}

const VelocityMetric: FC<VelocityMetricProps> = ({
  completed,
  total,
  tooltip = SizingMethod.StoryPoints,
  cardStyle,
}) => {
  const { token } = useTheme()

  const velocityPercentage =
    total > 0 ? `${((completed / total) * 100).toFixed(1)}%` : '0%'

  const resolvedTooltip = isSizingMethod(tooltip)
    ? getTooltipText(tooltip)
    : tooltip

  return (
    <MetricCard
      title="Velocity"
      value={completed}
      valueStyle={{ color: token.colorSuccess }}
      secondaryValue={velocityPercentage}
      tooltip={resolvedTooltip}
      cardStyle={cardStyle}
    />
  )
}

export default VelocityMetric
