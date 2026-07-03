'use client'

import {
  WaydGrid2,
  renderPortfolioLink,
  renderProgramLink,
  renderProjectLink,
} from '@/src/components/common/wayd-grid2'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, ReactNode, useMemo } from 'react'

export interface ProjectsGridProps {
  projects: ProjectListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  hideProgram?: boolean
  gridHeight?: number | undefined
  viewSelector?: ReactNode | undefined
}

const ProjectsGrid: FC<ProjectsGridProps> = (props: ProjectsGridProps) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<ProjectListDto, any>[]>(
    () => [
      {
        id: 'position',
        accessorKey: 'position',
        header: 'Rank',
        size: 90,
        enableColumnFilter: false,
        cell: ({ getValue }) => {
          const value = getValue() as number | null | undefined
          return value == null ? '—' : String(value)
        },
      },
      { id: 'key', accessorKey: 'key', header: 'Key', size: 125 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderProjectLink(row.original),
      },
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
        cell: ({ row }) =>
          row.original.status ? (
            <LifecycleStatusTag status={row.original.status} />
          ) : null,
      },
      {
        id: 'health',
        accessorFn: (row) => row.healthCheck?.status?.name ?? '',
        header: 'Health',
        size: 125,
        cell: ({ row }) => {
          const project = row.original
          if (!project.healthCheck) return null
          return (
            <ProjectHealthCheckTag
              healthCheck={project.healthCheck}
              projectId={project.id}
            />
          )
        },
      },
      {
        id: 'portfolio',
        accessorKey: 'portfolio.name',
        header: 'Portfolio',
        size: 200,
        meta: { hide: props.hidePortfolio, filterEnableSet: true },
        cell: ({ row }) => renderPortfolioLink(row.original.portfolio),
      },
      {
        id: 'program',
        accessorKey: 'program.name',
        header: 'Program',
        size: 200,
        meta: { hide: props.hideProgram, filterEnableSet: true },
        cell: ({ row }) => renderProgramLink(row.original.program),
      },
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start',
        size: 125,
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        size: 125,
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'projectManagers',
        accessorFn: (row) => getSortedNames(row.projectManagers ?? []),
        header: 'PMs',
      },
      {
        id: 'projectOwners',
        accessorFn: (row) => getSortedNames(row.projectOwners ?? []),
        header: 'Owners',
      },
      {
        id: 'projectSponsors',
        accessorFn: (row) => getSortedNames(row.projectSponsors ?? []),
        header: 'Sponsors',
      },
      {
        id: 'strategicThemes',
        accessorFn: (row) => getSortedNames(row.strategicThemes ?? []),
        header: 'Strategic Themes',
      },
      {
        id: 'projectLifecycle',
        accessorKey: 'projectLifecycle.name',
        header: 'Lifecycle',
        meta: { filterType: 'set' },
      },
    ],
    [props.hidePortfolio, props.hideProgram],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid2
      columns={columns}
      data={props.projects}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="projects"
      rightSlot={props.viewSelector}
      height={props.gridHeight}
      emptyMessage="No projects found."
    />
  )
}

export default ProjectsGrid
