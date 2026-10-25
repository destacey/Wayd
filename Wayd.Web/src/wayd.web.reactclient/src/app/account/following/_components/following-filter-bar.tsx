'use client'

import { Select, Tag } from 'antd'
import { FollowCountsDto } from '@/src/services/wayd-api'
import { typeLabel } from '@/src/components/common/record/record-identity'
import styles from '../following.module.css'
import { FollowingFilter, followViews, viewCount } from './following-filters'

export interface FollowingFilterBarProps {
  counts: FollowCountsDto
  filter: FollowingFilter
  onChange: (filter: FollowingFilter) => void
}

const Everything = '__everything__'

/**
 * The rail folded into a row for narrower screens: the type as a select and the
 * "needs a look" views as chips. Unlike the rail, the two combine.
 */
const FollowingFilterBar = ({
  counts,
  filter,
  onChange,
}: FollowingFilterBarProps) => (
  <div className={styles.filterBar}>
    <Select
      aria-label="Type"
      style={{ minWidth: 200 }}
      value={filter.ownerType ?? Everything}
      onChange={(value) =>
        onChange({
          ...filter,
          ownerType: value === Everything ? undefined : value,
        })
      }
      options={[
        { value: Everything, label: `Everything (${counts.total})` },
        ...counts.types.map((type) => ({
          value: type.ownerType,
          label: `${typeLabel(type.ownerType, type.displayName)} (${type.count})`,
        })),
      ]}
    />
    {followViews.map(({ view, label }) => (
      <Tag.CheckableTag
        key={view}
        checked={filter.view === view}
        onChange={(checked) =>
          onChange({ ...filter, view: checked ? view : undefined })
        }
      >
        {label} ({viewCount(counts, view)})
      </Tag.CheckableTag>
    ))}
  </div>
)

export default FollowingFilterBar
