'use client'

import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintDetailsDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import SprintMetrics from './sprint-metrics'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { FC, ReactNode } from 'react'

export interface SprintDetailsProps {
  sprint: SprintDetailsDto
  sizingMethod?: SizingMethod
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

const SprintDetails: FC<SprintDetailsProps> = ({
  sprint,
  sizingMethod,
  onHealthIndicatorReady,
}: SprintDetailsProps) => {
  if (!sprint) return null

  const sprintState = sprint.state.id as IterationState
  const showMetrics =
    sprintState === IterationState.Active ||
    sprintState === IterationState.Completed

  return (
    <Flex vertical gap={16}>
      {/* Team and dates live in the record's details panel — repeating them
          here would duplicate the panel beside it. */}
      <TimelineProgress
        start={sprint.start}
        end={sprint.end}
        dateFormat="MMM D, YYYY h:mm A"
      />
      {showMetrics && (
        <SprintMetrics
          sprint={sprint}
          sizingMethod={sizingMethod}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />
      )}
    </Flex>
  )
}

export default SprintDetails
