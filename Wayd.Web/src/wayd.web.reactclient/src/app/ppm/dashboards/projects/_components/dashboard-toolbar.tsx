'use client'

import { SearchOutlined } from '@ant-design/icons'
import PpmViewSelector, {
  PpmView,
} from '@/src/app/ppm/_components/ppm-view-selector'
import { Flex, Input, Segmented, Select } from 'antd'
import { FC } from 'react'
import { GroupBy, SortBy } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export type DashboardView = PpmView

export interface DashboardToolbarProps {
  groupBy: GroupBy
  onGroupByChange: (groupBy: GroupBy) => void
  sortBy: SortBy
  onSortByChange: (sortBy: SortBy) => void
  /** The List view sorts by its column headers, so the select is for the others. */
  showSort: boolean
  search: string
  onSearchChange: (search: string) => void
  view: DashboardView
  onViewChange: (view: DashboardView) => void
  shownCount: number
  totalCount: number
}

const GROUP_OPTIONS: { label: string; value: GroupBy }[] = [
  { label: 'Portfolio', value: 'portfolio' },
  { label: 'Program', value: 'program' },
  { label: 'Health', value: 'health' },
  { label: 'Status', value: 'status' },
]

const SORT_OPTIONS: { label: string; value: SortBy }[] = [
  { label: 'Health, then overdue', value: 'attention' },
  { label: 'Name', value: 'name' },
  { label: 'End date', value: 'end' },
  { label: 'Score', value: 'score' },
]

const DashboardToolbar: FC<DashboardToolbarProps> = ({
  groupBy,
  onGroupByChange,
  sortBy,
  onSortByChange,
  showSort,
  search,
  onSearchChange,
  view,
  onViewChange,
  shownCount,
  totalCount,
}) => (
  <Flex className={styles.toolbar} align="center" gap={12} wrap>
    <Flex align="center" gap={6}>
      <span className={styles.scopeLabel}>Group by</span>
      <Segmented
        size="small"
        options={GROUP_OPTIONS}
        value={groupBy}
        onChange={(value) => onGroupByChange(value as GroupBy)}
      />
    </Flex>
    {showSort && (
      <Flex align="center" gap={6}>
        <span className={styles.scopeLabel}>Sort</span>
        <Select
          size="small"
          options={SORT_OPTIONS}
          value={sortBy}
          onChange={onSortByChange}
          style={{ width: 170 }}
          aria-label="Sort projects"
        />
      </Flex>
    )}
    <Input
      className={styles.searchInput}
      size="small"
      allowClear
      prefix={<SearchOutlined />}
      placeholder="Search projects"
      aria-label="Search projects"
      value={search}
      onChange={(e) => onSearchChange(e.target.value)}
    />
    <span className={styles.shownCount}>
      {shownCount === totalCount
        ? `${totalCount} ${totalCount === 1 ? 'project' : 'projects'}`
        : `${shownCount} of ${totalCount} shown`}
    </span>
    <PpmViewSelector
      className={styles.viewSwitch}
      views={['Card', 'List', 'Timeline']}
      value={view}
      onChange={onViewChange}
    />
  </Flex>
)

export default DashboardToolbar
