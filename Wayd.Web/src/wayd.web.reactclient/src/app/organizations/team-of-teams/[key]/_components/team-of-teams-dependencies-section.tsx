'use client'

import { DependencyWheelExplorer } from '@/src/components/common/dependency-wheel'
import { useGetTeamOfTeamsDependenciesQuery } from '@/src/store/features/organizations/team-api'

export interface TeamOfTeamsDependenciesSectionProps {
  teamOfTeamsId: string
  teamOfTeamsKey: number
}

/**
 * The open dependencies that cross team lines for the teams beneath a team of teams: a wheel of where they
 * run between teams, inside the team of teams and out of it, over the list of them.
 */
const TeamOfTeamsDependenciesSection = ({
  teamOfTeamsId,
  teamOfTeamsKey,
}: TeamOfTeamsDependenciesSectionProps) => {
  const { data, isLoading, refetch } =
    useGetTeamOfTeamsDependenciesQuery(teamOfTeamsId)

  return (
    <DependencyWheelExplorer
      dependencies={data ?? []}
      isLoading={isLoading}
      refetch={refetch}
      gridKey="team-of-teams-dependencies"
      exportFileName={`team-of-teams-${teamOfTeamsKey}-dependencies`}
      emptyMessage="No open dependencies cross team lines for these teams."
    />
  )
}

export default TeamOfTeamsDependenciesSection
