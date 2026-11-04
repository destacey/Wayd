'use client'

import { DependencyPartnersExplorer } from '@/src/components/common/work/dependency-partners'
import { TeamDetailsDto } from '@/src/services/wayd-api'
import { useGetTeamDependenciesQuery } from '@/src/store/features/organizations/team-api'
import { FC } from 'react'

export interface TeamDependencyManagementProps {
  team: TeamDetailsDto
}

/**
 * A team's open dependencies: a chart of the other teams it shares them with, over the list of them.
 */
const TeamDependencyManagement: FC<TeamDependencyManagementProps> = (props) => {
  const teamId = props.team?.id

  const { data, isLoading, refetch } = useGetTeamDependenciesQuery(teamId, {
    skip: !teamId,
  })

  return (
    <DependencyPartnersExplorer
      teamId={teamId}
      dependencies={data ?? []}
      isLoading={isLoading}
      refetch={refetch}
      gridKey="team-dependencies"
    />
  )
}

export default TeamDependencyManagement
