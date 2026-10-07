'use client'

import { FC } from 'react'
import { MetricCard } from '.'
import useTheme from '@/src/components/contexts/theme'

export interface HealthMetricProps {
  value: number
  title: string
  tooltip?: string
  goodIfZero?: boolean
  /** How a bad value is shown: an error, or a warning for a gap worth fixing that doesn't break anything. */
  severity?: 'error' | 'warning'
  cardStyle?: React.CSSProperties
}

const HealthMetric: FC<HealthMetricProps> = ({
  value,
  title,
  tooltip,
  goodIfZero = true,
  severity = 'error',
  cardStyle,
}) => {
  const { token } = useTheme()

  const isGood = goodIfZero ? value === 0 : value > 0
  const badColor =
    severity === 'warning' ? token.colorWarning : token.colorError
  const color = isGood ? token.colorSuccess : badColor

  return (
    <MetricCard
      title={title}
      value={value}
      valueStyle={{ color }}
      tooltip={tooltip}
      cardStyle={cardStyle}
    />
  )
}

export default HealthMetric
