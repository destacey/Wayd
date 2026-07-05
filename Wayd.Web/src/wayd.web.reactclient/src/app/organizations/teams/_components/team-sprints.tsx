'use client'

import { SprintsGrid } from '@/src/components/common/planning'
import { useGetTeamSprintsQuery } from '@/src/store/features/organizations/team-api'
import { FC } from 'react'

export interface TeamSprintsProps {
  teamId: string
}

const TeamSprints: FC<TeamSprintsProps> = (props) => {
  const {
    data: sprintData,
    isLoading,
    refetch,
  } = useGetTeamSprintsQuery(props.teamId, { skip: !props.teamId })

  return (
    <SprintsGrid
      sprints={sprintData ?? []}
      isLoading={isLoading}
      refetch={refetch}
      hideTeam={true}
      persistStateKey="team-sprints"
    />
  )
}

export default TeamSprints
