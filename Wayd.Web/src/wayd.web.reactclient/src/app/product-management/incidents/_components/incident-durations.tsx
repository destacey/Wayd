'use client'

import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import { Flex } from 'antd'
import { formatIncidentDuration } from './incident-format'

export interface IncidentDurationValues {
  noticedToResponseStartedMilliseconds?: number | null
  responseToRecoveredMilliseconds?: number | null
  activeMilliseconds?: number | null
}

const STRETCHES = [
  {
    field: 'noticedToResponseStartedMilliseconds',
    label: 'Noticed to response',
    tooltip: 'From when it was noticed until response started.',
  },
  {
    field: 'responseToRecoveredMilliseconds',
    label: 'Response to recovery',
    tooltip: 'From when response started until it recovered. A canceled incident has none.',
  },
  {
    field: 'activeMilliseconds',
    label: 'Active',
    tooltip: 'From when it was noticed until it recovered, or until now while it is still active.',
  },
] as const

/**
 * The stretches the record can already answer, as the same metric tiles the
 * incidents grid uses. A stretch that has not started is omitted. Zero is a
 * real duration and stays visible.
 */
const IncidentDurations = ({ durations }: { durations: IncidentDurationValues }) => {
  const tiles = STRETCHES.flatMap((stretch) => {
    const milliseconds = durations[stretch.field]
    if (milliseconds == null) {
      return []
    }

    return [
      <MetricCard
        key={stretch.field}
        title={stretch.label}
        value={formatIncidentDuration(milliseconds)}
        tooltip={stretch.tooltip}
        cardStyle={METRIC_CARD_FLEX}
      />,
    ]
  })

  if (tiles.length === 0) {
    return null
  }

  return (
    <Flex gap="small" wrap>
      {tiles}
    </Flex>
  )
}

export default IncidentDurations
