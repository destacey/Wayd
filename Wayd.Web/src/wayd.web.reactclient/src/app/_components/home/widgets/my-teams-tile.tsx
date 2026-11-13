'use client'

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import { widgetAnchorId } from '../widget-registry'

/** The Teams list, where both team widgets' "View all" goes. */
export const TEAMS_HREF = '/organizations/teams'

/**
 * The summary the My teams and My teams of teams widgets and their tiles read;
 * RTK Query shares the request. Skipped when the home context says the caller
 * is on no team and no team of teams.
 * Kept apart from the widgets, which load after the page, so the tiles can
 * count without pulling the widgets' code in with them.
 */
export const useMyTeamsSummary = () => {
  const { context } = useHomeContext()
  const isOnNoTeam =
    context !== undefined &&
    context.teams.teamCount + context.teams.teamOfTeamsCount === 0
  return {
    ...useGetMyTeamsSummaryQuery(undefined, { skip: isOnNoTeam }),
    isOnNoTeam,
  }
}

/** Open dependencies at risk or unhealthy across the caller's teams. */
export const DependenciesAtRiskTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  // No count without View Work Items reads as zero, so the tile gives up its slot.
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.dependenciesAtRiskCount ?? 0)
  return (
    <AttentionTile
      title="Dependencies at risk"
      context="on your teams"
      value={value}
      tooltip="Open dependencies of your teams that are at risk (neither end planned) or unhealthy (planned to finish after they are needed). A dependency between two of your teams counts once."
      href={`#${widgetAnchorId('my-teams')}`}
      alert="warning"
      loading={isLoading}
    />
  )
}

/** Open cross-team dependencies that are unhealthy across the caller's teams of teams. */
export const CrossTeamDependenciesTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.unhealthyCrossTeamDependencyCount ?? 0)
  return (
    <AttentionTile
      title="Unhealthy dependencies"
      context="across your teams of teams"
      value={value}
      tooltip="Open dependencies between teams in your teams of teams that are unhealthy: planned to finish after they are needed, or whose successor started first. A dependency shared by two of them counts once."
      href={`#${widgetAnchorId('my-teams-of-teams')}`}
      alert="error"
      loading={isLoading}
    />
  )
}

/** Sprints of the teams in the caller's teams of teams that were started and left open past their end. */
export const SprintsToCompleteTile: FC = () => {
  const { data, isLoading, isError, isOnNoTeam } = useMyTeamsSummary()
  const value = isError
    ? undefined
    : isOnNoTeam
      ? 0
      : (data?.sprintsEndedNotCompletedCount ?? 0)
  return (
    <AttentionTile
      title="Sprints to complete"
      context="in your teams of teams"
      value={value}
      tooltip="Sprints a team in your teams of teams started that are past their end and not yet completed. Open the team's sprint to complete it."
      href={`#${widgetAnchorId('my-teams-of-teams')}`}
      alert="error"
      loading={isLoading}
    />
  )
}
