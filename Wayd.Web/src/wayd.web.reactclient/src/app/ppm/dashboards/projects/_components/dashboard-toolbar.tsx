'use client'

import {
  AppstoreOutlined,
  MenuOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { Flex, Input, Segmented, Select } from 'antd'
import { FC } from 'react'
import { GroupBy, SortBy } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export type DashboardView = 'list' | 'cards'

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

const VIEW_OPTIONS = [
  { label: 'List', value: 'list', icon: <MenuOutlined /> },
  { label: 'Cards', value: 'cards', icon: <AppstoreOutlined /> },
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
    <Flex align="center" gap={6}>
      <span className={styles.scopeLabel}>Group by</span>
      <Segmented
        size="small"
        options={GROUP_OPTIONS}
        value={groupBy}
        onChange={(value) => onGroupByChange(value as GroupBy)}
      />
    </Flex>
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
    <Segmented
      className={styles.viewSwitch}
      size="small"
      options={VIEW_OPTIONS}
      value={view}
      onChange={(value) => onViewChange(value as DashboardView)}
      aria-label="View"
    />
  </Flex>
)

export default DashboardToolbar
