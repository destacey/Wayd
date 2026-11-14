'use client'

import { LabeledContent } from '@/src/components/common/content'
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
 * The stretches the record can already answer. A stretch that has not started
 * is omitted. Zero is a real duration and stays visible.
 */
const IncidentDurations = ({ durations }: { durations: IncidentDurationValues }) => {
  const rows = STRETCHES.flatMap((stretch) => {
    const milliseconds = durations[stretch.field]
    if (milliseconds == null) {
      return []
    }

    return [
      <LabeledContent key={stretch.field} label={stretch.label} tooltip={stretch.tooltip}>
        {formatIncidentDuration(milliseconds)}
      </LabeledContent>,
    ]
  })

  if (rows.length === 0) {
    return null
  }

  return (
    <Flex gap="large" wrap>
      {rows}
    </Flex>
  )
}

export default IncidentDurations
