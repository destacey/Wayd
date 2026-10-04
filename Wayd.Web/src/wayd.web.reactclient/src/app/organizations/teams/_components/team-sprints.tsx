'use client'

import { SprintsGrid } from '@/src/components/common/planning'
import { useGetTeamSprintsQuery } from '@/src/store/features/organizations/team-api'
import { Alert } from 'antd'
import { FC } from 'react'
import { findOverlappingSprints, summarizeSprintNames } from './sprint-overlaps'

export interface TeamSprintsProps {
  teamId: string
}

const TeamSprints: FC<TeamSprintsProps> = (props) => {
  const {
    data: sprintData,
    isLoading,
    refetch,
  } = useGetTeamSprintsQuery(props.teamId, { skip: !props.teamId })

  const overlapping = findOverlappingSprints(sprintData ?? [])

  return (
    // A fragment, not a flex column: WaydGrid collapses to no rows as the flex
    // item of an unsized flex container.
    <>
      {overlapping.length > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title="Some of this team's sprints have overlapping planned dates in Azure DevOps."
          description={`Overlapping: ${summarizeSprintNames(overlapping)}. Unless it is completed earlier, each overlapping sprint ends when the next sprint starts, not on its planned end.`}
        />
      )}
      <SprintsGrid
        sprints={sprintData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        hideTeam={true}
        persistStateKey="team-sprints"
      />
    </>
  )
}

export default TeamSprints
