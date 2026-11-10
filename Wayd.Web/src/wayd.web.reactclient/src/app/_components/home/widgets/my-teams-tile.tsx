'use client'

import { useGetMyTeamsSummaryQuery } from '@/src/store/features/user-management/me-api'
import { FC } from 'react'
import AttentionTile from '../attention-tile'
import { useHomeContext } from '../use-home-context'
import { widgetAnchorId } from '../widget-registry'

/**
 * The summary both the My teams widget and its tile read; RTK Query shares the
 * request. Skipped when the home context says the caller is on no team.
 * Kept apart from the widget, which loads after the page, so the tile can
 * count without pulling the widget's charts in with it.
 */
export const useMyTeamsSummary = () => {
  const { context } = useHomeContext()
  const isOnNoTeam = context?.teams.teamCount === 0
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
