'use client'

import { Flex, Segmented } from 'antd'
import { FC } from 'react'
import { GroupBy } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

const GROUP_OPTIONS: { label: string; value: GroupBy }[] = [
  { label: 'Portfolio', value: 'portfolio' },
  { label: 'Program', value: 'program' },
  { label: 'Health', value: 'health' },
  { label: 'Status', value: 'status' },
]

export interface GroupBySelectProps {
  value: GroupBy
  onChange: (groupBy: GroupBy) => void
}

/** The Group by switch, shared by the grid's toolbar and the cards/timeline bar. */
const GroupBySelect: FC<GroupBySelectProps> = ({ value, onChange }) => (
  <Flex align="center" gap={6}>
    <span className={styles.scopeLabel}>Group by</span>
    <Segmented
      size="small"
      options={GROUP_OPTIONS}
      value={value}
      onChange={(next) => onChange(next as GroupBy)}
    />
  </Flex>
)

export default GroupBySelect
