'use client'

import { WaydGrid } from '@/src/components/common'
import {
  LifecycleStatusTagCellRenderer,
  PortfolioLinkCellRenderer,
  ProgramLinkCellRenderer,
  NestedProjectHealthCheckStatusCellRenderer,
  ProjectLinkCellRenderer,
} from '@/src/components/common/wayd-grid-cell-renderers'
import { ProjectListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
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

  const columnDefs = useMemo<ColDef<ProjectListDto>[]>(
    () => [
      {
        field: 'position',
        headerName: 'Rank',
        headerTooltip:
          "Rank based on the project's portfolio and the current context.",
        width: 90,
        valueFormatter: (params) =>
          params.value == null ? '—' : String(params.value),
      },
      { field: 'key', width: 125 },
      {
        field: 'name',
        cellRenderer: ProjectLinkCellRenderer,
        width: 300,
        initialSort: 'asc',
      },
      {
        field: 'status.name',
        headerName: 'Status',
        width: 125,
        cellRenderer: LifecycleStatusTagCellRenderer,
      },
      {
        field: 'healthCheck.status.name',
        headerName: 'Health',
        width: 125,
        cellRenderer: NestedProjectHealthCheckStatusCellRenderer,
      },
      {
        field: 'portfolio.name',
        headerName: 'Portfolio',
        width: 200,
        hide: props.hidePortfolio,
        cellRenderer: (params: ICellRendererParams<ProjectListDto>) => {
          if (!params.data) return null
          return PortfolioLinkCellRenderer({
            ...(params as any),
            data: params.data.portfolio,
          })
        },
      },
      {
        field: 'program.name',
        headerName: 'Program',
        width: 200,
        hide: props.hideProgram,
        cellRenderer: (params: ICellRendererParams<ProjectListDto>) =>
          params.data?.program
            ? ProgramLinkCellRenderer({
                ...(params as any),
                data: params.data.program,
              })
            : null,
      },
      {
        field: 'start',
        width: 125,
        type: 'dateOnly',
      },
      {
        field: 'end',
        width: 125,
        type: 'dateOnly',
      },
      {
        field: 'projectManagers',
        headerName: 'PMs',
        valueGetter: (params) =>
          getSortedNames(params.data?.projectManagers ?? []),
      },
      {
        field: 'projectOwners',
        headerName: 'Owners',
        valueGetter: (params) =>
          getSortedNames(params.data?.projectOwners ?? []),
      },
      {
        field: 'projectSponsors',
        headerName: 'Sponsors',
        valueGetter: (params) =>
          getSortedNames(params.data?.projectSponsors ?? []),
      },
      {
        field: 'strategicThemes',
        headerName: 'Strategic Themes',
        valueGetter: (params) =>
          getSortedNames(params.data?.strategicThemes ?? []),
      },
      {
        field: 'projectLifecycle.name',
        headerName: 'Lifecycle',
      },
    ],
    [props.hidePortfolio, props.hideProgram],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <>
      <WaydGrid
        columnDefs={columnDefs}
        rowData={props.projects}
        loadData={refresh}
        loading={props.isLoading}
        toolbarActions={props.viewSelector}
        height={props.gridHeight}
        emptyMessage="No projects found."
      />
    </>
  )
}

export default ProjectsGrid
