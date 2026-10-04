'use client'

import { CSSProperties, FC } from 'react'
import { MetricCard } from '.'
import useTheme from '@/src/components/contexts/theme'
import { SizingMethod } from '@/src/services/wayd-api'
import { isSizingMethod, sizingMethodMeasure } from '@/src/utils'

const getTooltipText = (sizingMethod: SizingMethod): string =>
  `Percentage of work that is completed (Done or Removed), measured in ${sizingMethodMeasure(sizingMethod)}.`

export interface CompletionRateMetricProps {
  completed: number
  total: number
  target?: number
  title?: string
  tooltip?: string | SizingMethod
  cardStyle?: CSSProperties
}

const CompletionRateMetric: FC<CompletionRateMetricProps> = ({
  completed,
  total,
  target = 80,
  title = 'Completion Rate',
  tooltip = SizingMethod.StoryPoints,
  cardStyle,
}) => {
  const { token } = useTheme()

  const completionRate =
    total > 0 ? Number(((completed / total) * 100).toFixed(1)) : 0

  const resolvedTooltip = isSizingMethod(tooltip)
    ? getTooltipText(tooltip)
    : tooltip

  return (
    <MetricCard
      title={title}
      value={completionRate}
      precision={1}
      suffix="%"
      valueStyle={{
        color: completionRate >= target ? token.colorSuccess : undefined,
      }}
      tooltip={resolvedTooltip}
      cardStyle={cardStyle}
    />
  )
}

export default CompletionRateMetric
