'use client'

import { RightOutlined } from '@ant-design/icons'
import { FC } from 'react'
import { ProjectGroup } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export interface DashboardGroupHeaderProps {
  group: ProjectGroup
  collapsed: boolean
  onToggle: () => void
}

/** A collapsible group heading: name, count and the one-line attention summary. */
const DashboardGroupHeader: FC<DashboardGroupHeaderProps> = ({
  group,
  collapsed,
  onToggle,
}) => {
  const count = group.projects.length

  return (
    <button
      type="button"
      className={styles.groupHeader}
      onClick={onToggle}
      aria-expanded={!collapsed}
    >
      <RightOutlined
        className={`${styles.collapseIcon} ${collapsed ? '' : styles.collapseIconExpanded}`}
      />
      <span className={styles.groupName}>{group.name}</span>
      <span className={styles.groupMeta}>
        {count} {count === 1 ? 'project' : 'projects'} · {group.summary}
      </span>
    </button>
  )
}

export default DashboardGroupHeader
