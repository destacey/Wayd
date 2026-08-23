'use client'

import { FC, ReactNode } from 'react'
import { MetricCard } from '.'

export interface CycleTimeMetricProps {
  value: number
  title?: string
  tooltip?: string
  cardStyle?: React.CSSProperties
  embedded?: boolean
  /** Qualifier shown bottom-right — e.g. the window the average covers. */
  secondaryValue?: ReactNode
  /** Makes the card a link to the report this figure summarises. */
  onClick?: () => void
}

const CycleTimeMetric: FC<CycleTimeMetricProps> = ({
  value,
  title = 'Avg Cycle Time',
  tooltip = 'The time from when work starts (Activated) to when it is completed (Done).',
  cardStyle,
  embedded,
  secondaryValue,
  onClick,
}) => {
  return (
    <MetricCard
      title={title}
      value={value}
      precision={2}
      suffix="days"
      tooltip={tooltip}
      cardStyle={cardStyle}
      embedded={embedded}
      secondaryValue={secondaryValue}
      onClick={onClick}
    />
  )
}

export default CycleTimeMetric
