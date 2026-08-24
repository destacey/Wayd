'use client'

import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintDetailsDto } from '@/src/services/wayd-api'
import { Descriptions, Flex } from 'antd'
import Link from 'next/link'
import SprintMetrics from './sprint-metrics'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { FC, ReactNode } from 'react'

const { Item: DescriptionItem } = Descriptions

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
      <Descriptions column={4}>
        <DescriptionItem label="Team">
          <Link href={`/organizations/teams/${sprint.team?.key}`}>
            {sprint.team?.name}
          </Link>
        </DescriptionItem>
      </Descriptions>
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
