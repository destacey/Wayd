'use client'

import { FollowCountsDto } from '@/src/services/wayd-api'
import styles from '../following.module.css'
import { FollowingFilter, followViews, viewCount } from './following-filters'

export interface FollowingRailProps {
  counts: FollowCountsDto
  filter: FollowingFilter
  onChange: (filter: FollowingFilter) => void
}

interface RailItemProps {
  label: string
  count: number
  selected: boolean
  onSelect: () => void
}

const RailItem = ({ label, count, selected, onSelect }: RailItemProps) => (
  <button
    type="button"
    className={`${styles.railItem} ${selected ? styles.railItemSelected : ''}`}
    aria-pressed={selected}
    onClick={onSelect}
  >
    <span>{label}</span>
    <span className={styles.railCount}>{count}</span>
  </button>
)

/**
 * The record types and "needs a look" views, each with its count. Choosing one
 * replaces the other: the rail narrows by a type or a view, not both.
 */
const FollowingRail = ({ counts, filter, onChange }: FollowingRailProps) => {
  const noView = filter.view === undefined

  return (
    <nav className={styles.rail} aria-label="Filter what you follow">
      <div className={styles.railHeading}>Type</div>
      <RailItem
        label="Everything"
        count={counts.total}
        selected={noView && filter.ownerType === undefined}
        onSelect={() => onChange({})}
      />
      {counts.types.map((type) => (
        <RailItem
          key={type.ownerType}
          label={type.displayName}
          count={type.count}
          selected={noView && filter.ownerType === type.ownerType}
          onSelect={() => onChange({ ownerType: type.ownerType })}
        />
      ))}
      <div className={styles.railHeading}>Needs a look</div>
      {followViews.map(({ view, label }) => (
        <RailItem
          key={view}
          label={label}
          count={viewCount(counts, view)}
          selected={filter.view === view}
          onSelect={() => onChange({ view })}
        />
      ))}
      <p className={styles.railNote}>
        Closed records are the ones worth pruning, so they get their own view.
      </p>
    </nav>
  )
}

export default FollowingRail
