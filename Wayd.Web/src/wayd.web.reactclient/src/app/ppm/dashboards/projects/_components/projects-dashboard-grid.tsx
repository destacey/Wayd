'use client'

import { LifecycleStatusTag } from '@/src/components/common'
import {
  renderPortfolioLink,
  renderProgramLink,
  renderProjectLink,
  WaydGrid,
  type GroupHeaderContext,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef, Row } from '@/src/components/common/wayd-grid-core'
import PpmViewSelector, {
  PpmView,
} from '@/src/app/ppm/_components/ppm-view-selector'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto } from '@/src/services/wayd-api'
import { Dayjs } from 'dayjs'
import { FC } from 'react'
import {
  collectLeadership,
  formatEnd,
  getEmployeeRoles,
  GroupBy,
  healthName,
  healthRank,
  isEndingSoon,
  NO_HEALTH_CHECK_LABEL,
  NO_PROGRAM_LABEL,
  PlanSummaries,
  statusRank,
  summarizeGroup,
} from './dashboard-model'
import GroupBySelect from './group-by-select'
import ProjectPlanLink from './project-plan-link'
import ProjectStatPills from './project-stat-pills'
import TeamAvatars from './team-avatars'
import styles from '../projects-dashboard.module.css'

export interface ProjectsDashboardGridProps {
  projects: ProjectListDto[]
  groupBy: GroupBy
  onGroupByChange: (groupBy: GroupBy) => void
  view: PpmView
  onViewChange: (view: PpmView) => void
  planSummaries: PlanSummaries
  /** Whose roles the Role column shows. Null drops the column. */
  employeeId: string | null
  selectedProjectKey: string | null
  onSelectProject: (key: string) => void
  isLoading: boolean
  today: Dayjs
  height?: number
  onRefresh: () => void
}

/** The grid column each Group by choice groups on. */
const GROUP_COLUMN: Record<GroupBy, string> = {
  portfolio: 'portfolio',
  program: 'program',
  health: 'health',
  status: 'status',
}

const nameList = (people: { name: string }[]) =>
  people.map((p) => p.name).join(', ')

const buildColumns = (
  planSummaries: PlanSummaries,
  employeeId: string | null,
  today: Dayjs,
): ColumnDef<ProjectListDto, any>[] => [
  {
    id: 'health',
    accessorFn: (row) => healthName(row),
    header: 'Health',
    size: 120,
    meta: { filterType: 'set' },
    // Worst first, so a sort or a grouping by health reads as a triage list.
    sortFn: (a: Row<ProjectListDto>, b: Row<ProjectListDto>) =>
      healthRank(a.original) - healthRank(b.original),
    cell: ({ row }) =>
      row.original.healthCheck ? (
        <ProjectHealthCheckTag
          healthCheck={row.original.healthCheck}
          projectId={row.original.id}
        />
      ) : (
        <span className={styles.muted}>{NO_HEALTH_CHECK_LABEL}</span>
      ),
  },
  { id: 'key', accessorKey: 'key', header: 'Key', size: 100 },
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
    size: 260,
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderProjectLink(row.original),
  },
  {
    id: 'portfolio',
    accessorKey: 'portfolio.name',
    header: 'Portfolio',
    size: 180,
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderPortfolioLink(row.original.portfolio),
  },
  {
    id: 'program',
    accessorFn: (row) => row.program?.name ?? NO_PROGRAM_LABEL,
    header: 'Program',
    size: 180,
    meta: { filterEnableSet: true },
    // Projects held directly by a portfolio sort last rather than under an
    // empty heading at the top.
    sortFn: (a: Row<ProjectListDto>, b: Row<ProjectListDto>) => {
      const aName = a.original.program?.name
      const bName = b.original.program?.name
      if (!aName && !bName) return 0
      if (!aName) return 1
      if (!bName) return -1
      return aName.localeCompare(bName, undefined, { sensitivity: 'base' })
    },
    cell: ({ row }) =>
      row.original.program ? (
        renderProgramLink(row.original.program)
      ) : (
        <span className={styles.muted}>{NO_PROGRAM_LABEL}</span>
      ),
  },
  {
    id: 'status',
    accessorKey: 'status.name',
    header: 'Status',
    size: 110,
    meta: { filterType: 'set' },
    sortFn: (a: Row<ProjectListDto>, b: Row<ProjectListDto>) =>
      statusRank(a.original.status.name) - statusRank(b.original.status.name),
    cell: ({ row }) => <LifecycleStatusTag status={row.original.status} />,
  },
  {
    id: 'stages',
    accessorFn: (row) =>
      (row.stages ?? [])
        .filter((s) => s.status.name === 'In Progress')
        .map((s) => s.name)
        .join(', '),
    header: 'Stages',
    size: 180,
    enableSorting: false,
    enableColumnFilter: false,
    cell: ({ row }) =>
      row.original.stages?.length > 0 ? (
        <StageTimeline stages={row.original.stages} displayMode="compact" />
      ) : (
        <span className={styles.muted}>No lifecycle</span>
      ),
  },
  {
    // One click to the plan, without opening the drawer or the project first.
    id: 'plan',
    header: 'Plan',
    size: 60,
    enableSorting: false,
    enableColumnFilter: false,
    meta: { enableExport: false, align: 'right' },
    cell: ({ row }) => <ProjectPlanLink project={row.original} />,
  },
  {
    id: 'overdue',
    accessorFn: (row) => planSummaries[row.id]?.overdue ?? 0,
    header: 'Tasks',
    size: 150,
    enableColumnFilter: false,
    cell: ({ row }) => (
      <ProjectStatPills summary={planSummaries[row.original.id]} />
    ),
  },
  {
    id: 'team',
    accessorFn: (row) =>
      nameList([
        ...(row.projectSponsors ?? []),
        ...(row.projectOwners ?? []),
        ...(row.projectManagers ?? []),
      ]),
    header: 'Team',
    size: 110,
    enableSorting: false,
    enableColumnFilter: false,
    cell: ({ row }) => (
      <TeamAvatars members={collectLeadership(row.original)} max={3} />
    ),
  },
  {
    id: 'start',
    accessorKey: 'start',
    header: 'Start',
    size: 120,
    // Off until chosen: the dashboard is about where projects are heading,
    // and the column chooser brings it back for anyone who wants it.
    meta: { columnType: 'dateOnly', hiddenByDefault: true },
  },
  {
    id: 'end',
    accessorKey: 'end',
    header: 'End',
    size: 120,
    meta: { columnType: 'dateOnly' },
    cell: ({ row }) => {
      const end = formatEnd(row.original.end)
      if (!end) return null
      return (
        <span
          className={
            isEndingSoon(row.original, today) ? styles.endDateSoon : undefined
          }
        >
          {end}
        </span>
      )
    },
  },
  {
    id: 'role',
    accessorFn: (row) => {
      const roles = getEmployeeRoles(row, employeeId)
      // Involvement with no project role can only be through an assigned task.
      return roles.length > 0 ? roles.join(' · ') : 'Task Assignee'
    },
    header: 'Role',
    size: 120,
    meta: { filterType: 'set', unavailable: employeeId === null },
  },
]

/**
 * The List view: a WaydGrid of the projects in scope, grouped by the Group
 * by choice. The grid's toolbar is the view's only toolbar: Group by sits in
 * its left slot and the view switch in its right, with the grid's own search,
 * sorting, filtering, column chooser and CSV export between. Clicking a row
 * opens the project drawer.
 */
const ProjectsDashboardGrid: FC<ProjectsDashboardGridProps> = ({
  projects,
  groupBy,
  onGroupByChange,
  view,
  onViewChange,
  planSummaries,
  employeeId,
  selectedProjectKey,
  onSelectProject,
  isLoading,
  today,
  height,
  onRefresh,
}) => {
  const renderGroupHeader = ({
    value,
    leafRows,
  }: GroupHeaderContext<ProjectListDto>) => (
    <>
      <span>{String(value)}</span>
      <span className={styles.groupMeta}>
        {leafRows.length} {leafRows.length === 1 ? 'project' : 'projects'} ·{' '}
        {summarizeGroup(leafRows, planSummaries)}
      </span>
    </>
  )

  return (
    <WaydGrid<ProjectListDto>
      data={projects}
      isLoading={isLoading}
      columns={buildColumns(planSummaries, employeeId, today)}
      getRowId={(project) => project.key}
      grouping={[GROUP_COLUMN[groupBy]]}
      renderGroupHeader={renderGroupHeader}
      initialSorting={[
        { id: 'health', desc: false },
        { id: 'overdue', desc: true },
      ]}
      onRowActivate={(project) => onSelectProject(project.key)}
      activatedRowId={selectedProjectKey}
      getRowActivateLabel={(project) => `${project.key} ${project.name}`}
      onRefresh={onRefresh}
      leftSlot={<GroupBySelect value={groupBy} onChange={onGroupByChange} />}
      rightSlot={
        <PpmViewSelector
          views={['Card', 'List', 'Timeline']}
          value={view}
          onChange={onViewChange}
        />
      }
      height={height}
      persistStateKey="projects-dashboard"
      csvFileName="projects-dashboard"
      emptyMessage="No projects match the current scope and filters."
    />
  )
}

export default ProjectsDashboardGrid
