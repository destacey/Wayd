'use client'

import { SearchOutlined } from '@ant-design/icons'
import PpmViewSelector, {
  PpmView,
} from '@/src/app/ppm/_components/ppm-view-selector'
import { Flex, Input, Select } from 'antd'
import { FC } from 'react'
import { GroupBy, SortBy } from './dashboard-model'
import GroupBySelect from './group-by-select'
import styles from '../projects-dashboard.module.css'

export type DashboardView = PpmView

export interface DashboardToolbarProps {
  groupBy: GroupBy
  onGroupByChange: (groupBy: GroupBy) => void
  sortBy: SortBy
  onSortByChange: (sortBy: SortBy) => void
  search: string
  onSearchChange: (search: string) => void
  view: DashboardView
  onViewChange: (view: DashboardView) => void
  shownCount: number
  totalCount: number
}

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
  search,
  onSearchChange,
  view,
  onViewChange,
  shownCount,
  totalCount,
}) => (
  <Flex className={styles.toolbar} align="center" gap={12} wrap>
    <GroupBySelect value={groupBy} onChange={onGroupByChange} />
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
