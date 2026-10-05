'use client'

import { WorkItemsBacklogGrid } from '@/src/components/common/work'
import { SizingMethod } from '@/src/services/wayd-api'
import { useGetTeamBacklogQuery } from '@/src/store/features/organizations/team-api'
import { FC } from 'react'

export interface TeamBacklogProps {
  teamId: string
  /** The team's current sizing method, whose estimate column shows by default. */
  sizingMethod?: SizingMethod
}

const TeamBacklog: FC<TeamBacklogProps> = ({ teamId, sizingMethod }) => {
  const backlogQuery = useGetTeamBacklogQuery(teamId, { skip: !teamId })

  return (
    <WorkItemsBacklogGrid
      workItems={backlogQuery.data ?? []}
      hideTeamColumn={true}
      sizingMethod={sizingMethod}
      isLoading={backlogQuery.isLoading}
      refetch={backlogQuery.refetch}
      persistStateKey="team-backlog"
    />
  )
}

export default TeamBacklog
