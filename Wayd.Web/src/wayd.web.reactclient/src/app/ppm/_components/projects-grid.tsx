'use client'

import {
  WaydGrid,
  createCsvColumn,
  renderPortfolioLink,
  renderProgramLink,
  renderProjectLink,
} from '@/src/components/common/wayd-grid'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import { ProjectListDto } from '@/src/services/wayd-api'
import { getSortedNameList } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, ReactNode } from 'react'

export interface ProjectsGridProps {
  projects: ProjectListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  hideProgram?: boolean
  gridHeight?: number | undefined
  viewSelector?: ReactNode | undefined
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const ProjectsGrid: FC<ProjectsGridProps> = (props: ProjectsGridProps) => {
  const { refetch } = props

  const columns: ColumnDef<ProjectListDto, any>[] = [
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
      meta: { filterType: 'set' },
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
    // Context-redundant columns are excluded from the defs (not meta.hide):
    // they never belong on the hosting page, so they shouldn't appear in
    // the column chooser or the persisted layout either.
    ...(props.hidePortfolio
      ? []
      : [
          {
            id: 'portfolio',
            accessorKey: 'portfolio.name',
            header: 'Portfolio',
            size: 200,
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderPortfolioLink(row.original.portfolio),
          } satisfies ColumnDef<ProjectListDto, any>,
        ]),
    ...(props.hideProgram
      ? []
      : [
          {
            id: 'program',
            accessorKey: 'program.name',
            header: 'Program',
            size: 200,
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderProgramLink(row.original.program),
          } satisfies ColumnDef<ProjectListDto, any>,
        ]),
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
    createCsvColumn<ProjectListDto>({
      id: 'projectManagers',
      header: 'PMs',
      getValues: (row) => getSortedNameList(row.projectManagers ?? []),
    }),
    createCsvColumn<ProjectListDto>({
      id: 'projectOwners',
      header: 'Owners',
      getValues: (row) => getSortedNameList(row.projectOwners ?? []),
    }),
    createCsvColumn<ProjectListDto>({
      id: 'projectSponsors',
      header: 'Sponsors',
      getValues: (row) => getSortedNameList(row.projectSponsors ?? []),
    }),
    createCsvColumn<ProjectListDto>({
      id: 'strategicThemes',
      header: 'Strategic Themes',
      getValues: (row) => getSortedNameList(row.strategicThemes ?? []),
    }),
    {
      id: 'projectLifecycle',
      accessorKey: 'projectLifecycle.name',
      header: 'Lifecycle',
      meta: { filterType: 'set' },
    },
  ]

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.projects}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="projects"
      rightSlot={props.viewSelector}
      height={props.gridHeight}
      persistStateKey={props.persistStateKey}
      emptyMessage="No projects found."
    />
  )
}

export default ProjectsGrid
