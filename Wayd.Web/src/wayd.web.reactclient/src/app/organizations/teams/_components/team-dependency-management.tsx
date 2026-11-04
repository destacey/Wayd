'use client'

import DependenciesGrid from '@/src/components/common/work/dependencies-grid'
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
    <DependenciesGrid
      dependencies={dependencyData ?? []}
      isLoading={isLoading}
      refetch={refetch}
      gridKey="team-dependencies"
    />
  )
}

export default TeamDependencyManagement
