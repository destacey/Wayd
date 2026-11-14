'use client'

import {
  MyTeamsSummaryResponse,
  TeamNavigationDto,
} from '@/src/services/wayd-api'
import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useWidgetSettingsOf } from '../home-settings-context'
import { useHomeContext } from '../use-home-context'
import { widgetAnchorId } from '../widget-registry'
import {
  pickedValues,
  WidgetChoiceSetting,
  WidgetPickOptions,
  WidgetPickOptionsSource,
  WidgetPickSetting,
  WidgetSettingValues,
} from '../widget-settings'

/** The Teams list, where both team widgets' "View all" goes. */
export const TEAMS_HREF = '/organizations/teams'

/** The key of My teams' choice of teams. */
export const TEAMS_SETTING_KEY = 'teams'
/** The key of My teams of teams' choice of teams of teams. */
export const TEAMS_OF_TEAMS_SETTING_KEY = 'teamsOfTeams'
/** The key of how My teams draws a running sprint. */
export const SPRINT_CARD_SETTING_KEY = 'sprintCard'

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * The ids a team pick stores that can be sent: the server refuses the whole
 * request over one that is not an id, so a damaged setting must not reach it.
 */
const teamIdsOf = (value: string | undefined) =>
  pickedValues(value).filter((id) => GUID.test(id))

/**
 * The summary the My teams and My teams of teams widgets and their tiles read.
 * RTK Query shares the request, so each asks with both widgets' choices, read
 * from the page rather than from a tile's own settings. The server ignores an
 * id the caller is no longer a member of.
 * Skipped when the home context says the caller is on no team and no team of teams.
 * Kept apart from the widgets, which load after the page, so the tiles can
 * count without pulling the widgets' code in with them.
 */
export const useMyTeamsSummary = () => {
  const { context } = useHomeContext()
  const teamIds = teamIdsOf(
    useWidgetSettingsOf('my-teams')?.[TEAMS_SETTING_KEY],
  )
  const teamOfTeamsIds = teamIdsOf(
    useWidgetSettingsOf('my-teams-of-teams')?.[TEAMS_OF_TEAMS_SETTING_KEY],
  )
  const isOnNoTeam =
    context !== undefined &&
    context.teams.teamCount + context.teams.teamOfTeamsCount === 0
  const query = useGetMyTeamsSummaryQuery(
    teamIds.length + teamOfTeamsIds.length > 0
      ? { teamIds, teamOfTeamsIds }
      : undefined,
    { skip: isOnNoTeam },
  )
  // While a changed choice loads, RTK Query keeps the last summary in `data`;
  // showing it would list and count a team just left out.
  const isChanging = !!query.isFetching && query.currentData === undefined
  return {
    ...query,
    data: isChanging ? undefined : query.data,
    isLoading: query.isLoading || isChanging,
    /**
     * The last summary loaded, kept while a changed choice loads: its lists of
     * every team the caller is on do not depend on the choice.
     */
    memberships: query.data,
    isOnNoTeam,
  }
}

/** Whether My teams shows only some of the caller's teams. */
export const showsSomeTeams = (summary: MyTeamsSummaryResponse | undefined) =>
  !!summary && summary.teams.length < (summary.allTeams?.length ?? 0)

/** Whether My teams of teams shows only some of the caller's teams of teams. */
export const showsSomeTeamsOfTeams = (
  summary: MyTeamsSummaryResponse | undefined,
) =>
  !!summary &&
  summary.teamsOfTeams.length < (summary.allTeamsOfTeams?.length ?? 0)

const asOptions = (
  teams: TeamNavigationDto[] | undefined,
  isLoading: boolean,
  isError: boolean,
): WidgetPickOptions => ({
  options: (teams ?? []).map((t) => ({ value: t.id, label: t.name })),
  isLoading: isLoading && !teams,
  isError: isError && !teams,
})

/** Every team the caller is on, for My teams' choice of teams. */
export const TeamOptions: WidgetPickOptionsSource = ({ children }) => {
  const { memberships, isLoading, isError } = useMyTeamsSummary()
  return children(asOptions(memberships?.allTeams, isLoading, isError))
}

/** Every team of teams the caller is on, for My teams of teams' choice. */
export const TeamOfTeamsOptions: WidgetPickOptionsSource = ({ children }) => {
  const { memberships, isLoading, isError } = useMyTeamsSummary()
  return children(asOptions(memberships?.allTeamsOfTeams, isLoading, isError))
}

/** Which of the caller's teams My teams shows. */
export const teamsPickSetting: WidgetPickSetting = {
  kind: 'pick',
  key: TEAMS_SETTING_KEY,
  label: 'Teams',
  noun: 'teams',
  Options: TeamOptions,
}

/** Which of the caller's teams of teams My teams of teams shows. */
export const teamsOfTeamsPickSetting: WidgetPickSetting = {
  kind: 'pick',
  key: TEAMS_OF_TEAMS_SETTING_KEY,
  label: 'Teams of teams',
  noun: 'teams of teams',
  Options: TeamOfTeamsOptions,
}

/** Whether a Scrum team's running sprint is drawn with its burn-up chart or as figures alone. */
export const sprintCardSetting: WidgetChoiceSetting = {
  kind: 'choice',
  key: SPRINT_CARD_SETTING_KEY,
  label: 'Sprint card',
  options: [
    { value: 'burnUp', label: 'With burn-up' },
    { value: 'figures', label: 'Figures only' },
  ],
  default: 'burnUp',
}

/** Whether My teams draws a running sprint's burn-up chart; it does for a widget rendered without settings. */
export const showsBurnUp = (settings: WidgetSettingValues | undefined) =>
  settings?.[SPRINT_CARD_SETTING_KEY] !== 'figures'

// The team tiles take no settings of their own: the request they share with
// both team widgets carries both widgets' choices, which `useMyTeamsSummary`
// reads from the page.

/** Open dependencies at risk or unhealthy across the caller's teams. */
export const DependenciesAtRiskTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  const teams = showsSomeTeams(data) ? 'the teams shown' : 'your teams'
  // No count without View Work Items reads as zero, so the tile gives up its slot.
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.dependenciesAtRiskCount ?? 0)
  return (
    <AttentionTile
      title="Dependencies at risk"
      context={`on ${teams}`}
      value={value}
      tooltip={`Open dependencies of ${teams} that are at risk (neither end planned) or unhealthy (planned to finish after they are needed). A dependency between two of them counts once.`}
      href={`#${widgetAnchorId('my-teams')}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

/** Open cross-team dependencies that are unhealthy across the caller's teams of teams. */
export const CrossTeamDependenciesTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  const teamsOfTeams = showsSomeTeamsOfTeams(data)
    ? 'the teams of teams shown'
    : 'your teams of teams'
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.unhealthyCrossTeamDependencyCount ?? 0)
  return (
    <AttentionTile
      title="Unhealthy dependencies"
      context={`across ${teamsOfTeams}`}
      value={value}
      tooltip={`Open dependencies between teams in ${teamsOfTeams} that are unhealthy: planned to finish after they are needed, or whose successor started first. A dependency shared by two of them counts once.`}
      href={`#${widgetAnchorId('my-teams-of-teams')}`}
      alert="error"
      loading={isLoading}
    />
  )
}

/** Sprints of the teams in the caller's teams of teams that were started and left open past their end. */
export const SprintsToCompleteTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  const teamsOfTeams = showsSomeTeamsOfTeams(data)
    ? 'the teams of teams shown'
    : 'your teams of teams'
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.sprintsEndedNotCompletedCount ?? 0)
  return (
    <AttentionTile
      title="Sprints to complete"
      context={`in ${teamsOfTeams}`}
      value={value}
      tooltip={`Sprints a team in ${teamsOfTeams} started that are past their end and not yet completed. Open the team's sprint to complete it.`}
      href={`#${widgetAnchorId('my-teams-of-teams')}`}
      alert="error"
      loading={isLoading}
    />
  )
}
