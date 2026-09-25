'use client'

import { LifecycleStatusTag, WaydEmpty } from '@/src/components/common'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto } from '@/src/services/wayd-api'
import { Skeleton } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC, useState } from 'react'
import DashboardGroupHeader from './dashboard-group-header'
import {
  collectLeadership,
  getEmployeeRoles,
  isEndingSoon,
  PlanSummaries,
  ProjectGroup,
} from './dashboard-model'
import ProjectStatPills from './project-stat-pills'
import TeamAvatars from './team-avatars'
import styles from '../projects-dashboard.module.css'

export interface ProjectsDashboardListProps {
  groups: ProjectGroup[]
  planSummaries: PlanSummaries
  /** Whose roles the last column shows. Null hides the column. */
  employeeId: string | null
  selectedProjectKey: string | null
  onSelectProject: (key: string) => void
  isLoading: boolean
  today: Dayjs
  /** Fixed height for the scroll container; unset lets it grow with its rows. */
  height?: number
}

export const formatEnd = (end: Date | undefined) =>
  end ? dayjs(end).format('MMM D, YYYY') : null

interface RowProps {
  project: ProjectListDto
  planSummaries: PlanSummaries
  employeeId: string | null
  isSelected: boolean
  onSelect: (key: string) => void
  today: Dayjs
}

const ProjectRow: FC<RowProps> = ({
  project,
  planSummaries,
  employeeId,
  isSelected,
  onSelect,
  today,
}) => {
  const roles = getEmployeeRoles(project, employeeId)
  const sub = project.program
    ? `${project.portfolio.name} · ${project.program.name}`
    : project.portfolio.name
  const endingSoon = isEndingSoon(project, today)

  const rowClass = [
    styles.row,
    employeeId ? '' : styles.rowNoRoles,
    isSelected ? styles.rowSelected : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      type="button"
      className={rowClass}
      onClick={() => onSelect(project.key)}
      aria-pressed={isSelected}
      aria-label={`${project.key} ${project.name}`}
    >
      <span className={styles.cell}>
        <ProjectHealthCheckTag
          healthCheck={project.healthCheck}
          projectId={project.id}
          variant="tag"
        />
      </span>
      <span className={`${styles.cell} ${styles.key}`}>{project.key}</span>
      <span className={styles.cell}>
        <div className={styles.projectName}>{project.name}</div>
        <div className={styles.projectSub}>{sub}</div>
      </span>
      <span className={styles.cell}>
        <LifecycleStatusTag status={project.status} />
      </span>
      <span className={styles.cell}>
        {project.stages?.length > 0 ? (
          <StageTimeline stages={project.stages} displayMode="small" />
        ) : (
          <span className={`${styles.projectSub} ${styles.muted}`}>
            No lifecycle
          </span>
        )}
      </span>
      <span className={styles.cell}>
        <ProjectStatPills summary={planSummaries[project.id]} />
      </span>
      <span className={styles.cell}>
        <TeamAvatars members={collectLeadership(project)} max={3} />
      </span>
      <span
        className={`${styles.cell} ${styles.endDate} ${endingSoon ? styles.endDateSoon : ''}`}
      >
        {formatEnd(project.end) ?? <span className={styles.muted}>—</span>}
      </span>
      {employeeId && (
        <span className={`${styles.cell} ${styles.roles}`}>
          {roles.length > 0 ? roles.join(' · ') : 'Task Assignee'}
        </span>
      )}
    </button>
  )
}

interface GroupSectionProps extends Omit<RowProps, 'project' | 'isSelected'> {
  group: ProjectGroup
  selectedProjectKey: string | null
}

const GroupSection: FC<GroupSectionProps> = ({
  group,
  selectedProjectKey,
  ...rowProps
}) => {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <div>
      <DashboardGroupHeader
        group={group}
        collapsed={collapsed}
        onToggle={() => setCollapsed((c) => !c)}
      />
      {!collapsed &&
        group.projects.map((project) => (
          <ProjectRow
            key={project.key}
            project={project}
            isSelected={selectedProjectKey === project.key}
            {...rowProps}
          />
        ))}
    </div>
  )
}

/**
 * The dense grouped table. Every row is one project and opens the detail
 * drawer; group headers collapse. Columns are fixed rather than a WaydGrid
 * because the grid has no row grouping, and this view is about scanning
 * groups rather than sorting columns.
 */
const ProjectsDashboardList: FC<ProjectsDashboardListProps> = ({
  groups,
  planSummaries,
  employeeId,
  selectedProjectKey,
  onSelectProject,
  isLoading,
  today,
  height,
}) => {
  if (isLoading) {
    return (
      <div className={styles.list} style={{ padding: 16 }}>
        <Skeleton active paragraph={{ rows: 6 }} />
      </div>
    )
  }

  if (groups.length === 0) {
    return (
      <div className={`${styles.list} ${styles.emptyState}`}>
        <WaydEmpty message="No projects match the current scope and filters." />
      </div>
    )
  }

  return (
    <div className={styles.list} style={{ height }}>
      <div
        className={`${styles.header} ${employeeId ? '' : styles.headerNoRoles}`}
        role="row"
      >
        <span>Health</span>
        <span>Key</span>
        <span>Project</span>
        <span>Status</span>
        <span>Stages</span>
        <span>Tasks</span>
        <span>Team</span>
        <span>End</span>
        {employeeId && <span>Role</span>}
      </div>
      {groups.map((group) => (
        <GroupSection
          key={group.key}
          group={group}
          selectedProjectKey={selectedProjectKey}
          planSummaries={planSummaries}
          employeeId={employeeId}
          onSelect={onSelectProject}
          today={today}
        />
      ))}
    </div>
  )
}

export default ProjectsDashboardList
