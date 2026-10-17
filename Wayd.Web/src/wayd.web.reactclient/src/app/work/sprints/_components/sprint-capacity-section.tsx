'use client'

import { Button, Flex } from 'antd'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import TeamCapacityPlan from '@/src/app/organizations/_components/capacity-plan/team-capacity-plan'
import SprintCapacitySummary from './sprint-capacity-summary'

export interface SprintCapacitySectionProps {
  sprint: SprintDetailsDto
  /** Opens the sprint's team days off; absent for those who cannot change them. */
  onSetTeamDaysOff?: () => void
}

/**
 * The sprint's capacity plan: its team's people over the sprint's planned
 * dates, beside its scope. Edits change the people's allocations to the team
 * for those weeks or days only, so the next sprint keeps what it had.
 */
const SprintCapacitySection = ({
  sprint,
  onSetTeamDaysOff,
}: SprintCapacitySectionProps) => (
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
      daysOff={sprint.teamDaysOff}
      extraActions={
        onSetTeamDaysOff && (
          <Button size="small" onClick={onSetTeamDaysOff}>
            Team Days Off
          </Button>
        )
      }
    />
  </Flex>
)

export default SprintCapacitySection
