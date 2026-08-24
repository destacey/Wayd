'use client'

import TeamDependenciesGrid from '@/src/components/common/organizations/team-dependencies-grid'
import { TeamDetailsDto } from '@/src/services/wayd-api'
import { useGetTeamDependenciesQuery } from '@/src/store/features/organizations/team-api'
import { FC } from 'react'

export interface TeamDependencyManagementProps {
  team: TeamDetailsDto
}

const TeamDependencyManagement: FC<TeamDependencyManagementProps> = (props) => {
  const {
    data: dependencyData,
    isLoading,
    refetch,
  } = useGetTeamDependenciesQuery(props.team?.id, { skip: !props.team?.id })

  return (
    <TeamDependenciesGrid
      team={props.team}
      dependencies={dependencyData ?? []}
      isLoading={isLoading}
      refetch={refetch}
    />
  )
}

export default TeamDependencyManagement
