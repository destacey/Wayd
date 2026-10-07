'use client'

import { IterationState } from '@/src/components/types'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import { Alert, Flex } from 'antd'
import SprintMetrics from './sprint-metrics'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import { sprintActiveDays } from '@/src/utils'
import { FC, ReactNode } from 'react'

export interface SprintDetailsProps {
  sprint: SprintDetailsDto
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

// Overlapping sprints would count the same time twice, so an earlier sprint
// the team did not complete ends when the later one starts.
export const sprintOverlapWarning = (
  sprint: SprintDetailsDto,
): { title: string; description: string } | null => {
  const neighbours = [
    sprint.overlapsPreviousSprint && 'previous',
    sprint.overlapsNextSprint && 'next',
  ].filter(Boolean)
  if (!neighbours.length) return null

  const cuts = [
    sprint.overlapsPreviousSprint &&
      'Unless it was completed earlier, the previous sprint ends when this sprint starts, not on its planned end.',
    sprint.overlapsNextSprint &&
      'Unless it is completed earlier, this sprint ends when the next sprint starts, not on its planned end.',
  ].filter(Boolean)

  return {
    title: `This sprint's planned dates overlap the team's ${neighbours.join(' and ')} sprint in Azure DevOps.`,
    description: `${cuts.join(' ')} Correct the dates in Azure DevOps to remove the overlap.`,
  }
}

const SprintDetails: FC<SprintDetailsProps> = ({
  sprint,
  onHealthIndicatorReady,
}: SprintDetailsProps) => {
  if (!sprint) return null

  const sprintState = sprint.state.id as IterationState
  const showMetrics =
    sprintState === IterationState.Active ||
    sprintState === IterationState.Completed

  const overlapWarning = sprintOverlapWarning(sprint)
  const activeDays = sprintActiveDays(sprint)

  return (
    <Flex vertical gap={16}>
      {overlapWarning && (
        <Alert
          type="warning"
          showIcon
          title={overlapWarning.title}
          description={overlapWarning.description}
        />
      )}
      {/* Team and dates live in the record's details panel — repeating them
          here would duplicate the panel beside it. */}
      <TimelineProgress
        start={activeDays.start}
        end={activeDays.end}
        dateFormat="MMM D, YYYY"
      />
      {showMetrics && (
        <SprintMetrics
          sprint={sprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />
      )}
    </Flex>
  )
}

export default SprintDetails
