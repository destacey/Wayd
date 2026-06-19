'use client'

import { WaydGrid } from '@/src/components/common'
import {
  LifecycleStatusTagCellRenderer,
  PortfolioLinkCellRenderer,
  ProgramLinkCellRenderer,
} from '@/src/components/common/wayd-grid-cell-renderers'
import { ProgramListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { FC, useMemo } from 'react'

export interface ProgramsGridProps {
  programs: ProgramListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  gridHeight?: number | undefined
  viewSelector?: React.ReactNode | undefined
}

const ProgramsGrid: FC<ProgramsGridProps> = (props: ProgramsGridProps) => {
  const { refetch } = props

  const columnDefs = useMemo<ColDef<ProgramListDto>[]>(
    () => [
      { field: 'key', width: 90 },
      {
        field: 'name',
        cellRenderer: ProgramLinkCellRenderer,
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
        field: 'portfolio.name',
        headerName: 'Portfolio',
        width: 200,
        hide: props.hidePortfolio,
        cellRenderer: (params: ICellRendererParams<ProgramListDto>) => {
          if (!params.data) return null
          return PortfolioLinkCellRenderer({ ...(params as any), data: params.data.portfolio })
        },
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
        field: 'programManagers',
        headerName: 'PMs',
        valueGetter: (params) => getSortedNames(params.data?.programManagers ?? []),
      },
      {
        field: 'programOwners',
        headerName: 'Owners',
        valueGetter: (params) => getSortedNames(params.data?.programOwners ?? []),
      },
      {
        field: 'programSponsors',
        headerName: 'Sponsors',
        valueGetter: (params) => getSortedNames(params.data?.programSponsors ?? []),
      },
      {
        field: 'strategicThemes',
        headerName: 'Strategic Themes',
        valueGetter: (params) => getSortedNames(params.data?.strategicThemes ?? []),
      },
    ],
    [props.hidePortfolio],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <>
      <WaydGrid
        columnDefs={columnDefs}
        rowData={props.programs}
        loadData={refresh}
        loading={props.isLoading}
        toolbarActions={props.viewSelector}
        height={props.gridHeight}
        emptyMessage="No programs found."
      />
    </>
  )
}

export default ProgramsGrid
