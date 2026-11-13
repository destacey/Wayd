'use client'

import { Flex } from 'antd'
import { FC } from 'react'
import WidgetCard, { WidgetEmpty, WidgetError } from '../widget-card'
import { HomeWidgetProps } from '../widget-registry'
import { TEAMS_HREF, useMyTeamsSummary } from './my-teams-tile'
import TeamOfTeamsRollup from './team-of-teams-rollup'

/** A rollup card for each team of teams the caller is a member of directly. */
const MyTeamsOfTeamsWidget: FC<HomeWidgetProps> = ({ title }) => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()

  const body = () => {
    if (isError)
      return <WidgetError message="Could not load your teams of teams." />
    if (isOnNoTeam || !data || data.teamsOfTeams.length === 0) {
      return (
        <WidgetEmpty message="You are not a member of a team of teams. Your teams of teams appear here once you join one." />
      )
    }
    return (
      <Flex vertical gap="middle">
        {data.teamsOfTeams.map((rollup) => (
          <TeamOfTeamsRollup key={rollup.team.id} rollup={rollup} />
        ))}
      </Flex>
    )
  }

  return (
    <WidgetCard
      title={title}
      viewAllHref={TEAMS_HREF}
      loading={isLoading}
      failed={isError}
      empty={isOnNoTeam || !data || data.teamsOfTeams.length === 0}
      emptySummary="Not on a team of teams"
    >
      {body()}
    </WidgetCard>
  )
}

export default MyTeamsOfTeamsWidget
