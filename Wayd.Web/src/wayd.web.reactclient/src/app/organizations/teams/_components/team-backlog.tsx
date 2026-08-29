'use client'

import { WorkItemsBacklogGrid } from '@/src/components/common/work'
import { useGetTeamBacklogQuery } from '@/src/store/features/organizations/team-api'
import { FC } from 'react'

export interface TeamBacklogProps {
  teamId: string
}

const TeamBacklog: FC<TeamBacklogProps> = ({ teamId }) => {
  const backlogQuery = useGetTeamBacklogQuery(teamId, { skip: !teamId })

  return (
    <WorkItemsBacklogGrid
      workItems={backlogQuery.data ?? []}
      hideTeamColumn={true}
      isLoading={backlogQuery.isLoading}
      refetch={backlogQuery.refetch}
      persistStateKey="team-backlog"
    />
  )
}

export default TeamBacklog
