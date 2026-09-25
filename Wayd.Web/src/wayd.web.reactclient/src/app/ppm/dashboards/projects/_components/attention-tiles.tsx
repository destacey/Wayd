'use client'

import { MetricCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { FC, RefObject } from 'react'
import {
  AttentionCounts,
  AttentionFilter,
  ENDING_SOON_DAYS,
} from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export interface AttentionTilesProps {
  counts: AttentionCounts
  active: AttentionFilter
  onChange: (filter: AttentionFilter) => void
  isLoading: boolean
  containerRef?: RefObject<HTMLDivElement | null>
}

interface TileDef {
  filter: AttentionFilter
  title: string
  value: (c: AttentionCounts) => number
  secondary?: (c: AttentionCounts) => string
  tooltip: string
  /** Colours the number once it is above zero. */
  alert?: 'error' | 'warning'
}

const TILES: TileDef[] = [
  {
    filter: 'all',
    title: 'In scope',
    value: (c) => c.inScope,
    tooltip: 'Projects matching the scope and filters above.',
  },
  {
    filter: 'unhealthy',
    title: 'Unhealthy',
    value: (c) => c.unhealthy,
    tooltip: 'Projects whose current health check is Unhealthy.',
    alert: 'error',
  },
  {
    filter: 'atRisk',
    title: 'At risk',
    value: (c) => c.atRisk,
    tooltip: 'Projects whose current health check is At Risk.',
    alert: 'warning',
  },
  {
    filter: 'overdue',
    title: 'Overdue tasks',
    value: (c) => c.overdueTasks,
    secondary: (c) =>
      c.overdueProjects === 0
        ? ''
        : `across ${c.overdueProjects} ${c.overdueProjects === 1 ? 'project' : 'projects'}`,
    tooltip: 'Open tasks past their planned end date.',
    alert: 'error',
  },
  {
    filter: 'noHealthCheck',
    title: 'No health check',
    value: (c) => c.noHealthCheck,
    tooltip:
      'Open projects with no current health check: never reported, or expired.',
  },
  {
    filter: 'endingSoon',
    title: `Ending in ${ENDING_SOON_DAYS} days`,
    value: (c) => c.endingSoon,
    tooltip: `Open projects whose planned end is within the next ${ENDING_SOON_DAYS} days.`,
  },
]

/**
 * The triage row. Each tile is a filter on the list below it: click to keep
 * only those projects, click again to clear. Counts are computed from the
 * same list the tiles filter, so a tile never disagrees with what it shows.
 */
const AttentionTiles: FC<AttentionTilesProps> = ({
  counts,
  active,
  onChange,
  isLoading,
  containerRef,
}) => {
  const { token } = useTheme()

  return (
    <div ref={containerRef} className={styles.attentionRow}>
      {TILES.map((tile) => {
        const value = tile.value(counts)
        // "In scope" is the unfiltered state, so it never lights up as a filter.
        const isActive = active === tile.filter && tile.filter !== 'all'
        const alertColor =
          tile.alert === 'error' ? token.colorError : token.colorWarning
        return (
          <MetricCard
            key={tile.filter}
            title={tile.title}
            value={value}
            tooltip={tile.tooltip}
            loading={isLoading}
            secondaryValue={tile.secondary?.(counts) || undefined}
            valueStyle={
              tile.alert && value > 0 ? { color: alertColor } : undefined
            }
            cardStyle={{
              borderColor: isActive ? token.colorPrimary : undefined,
              boxShadow: isActive
                ? `0 0 0 1px ${token.colorPrimary}`
                : undefined,
            }}
            onClick={() => onChange(isActive ? 'all' : tile.filter)}
            ariaLabel={`${tile.title}: ${value}${isActive ? ' (filtering)' : ''}`}
          />
        )
      })}
    </div>
  )
}

export default AttentionTiles
