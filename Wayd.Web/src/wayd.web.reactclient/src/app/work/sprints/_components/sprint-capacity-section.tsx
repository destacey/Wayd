'use client'

import { Flex } from 'antd'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import TeamCapacityPlan from '@/src/app/organizations/_components/capacity-plan/team-capacity-plan'
import SprintCapacitySummary from './sprint-capacity-summary'

export interface SprintCapacitySectionProps {
  sprint: SprintDetailsDto
}

/**
 * The sprint's capacity plan: its team's people over the sprint's planned
 * dates, beside its scope. Edits change the people's allocations to the team
 * for those weeks or days only, so the next sprint keeps what it had.
 */
const SprintCapacitySection = ({ sprint }: SprintCapacitySectionProps) => (
  <Flex vertical gap={16}>
    <SprintCapacitySummary sprintKey={sprint.key} />
    <TeamCapacityPlan
      team={{
        id: sprint.team.id,
        name: sprint.team.name,
        isActive: sprint.teamIsActive,
      }}
      teamType="Team"
      from={sprint.start}
      to={sprint.end}
    />
  </Flex>
)

export default SprintCapacitySection
