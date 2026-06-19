'use client'

import { WaydGrid } from '@/src/components/common'
import {
  LifecycleStatusTagCellRenderer,
  PortfolioLinkCellRenderer,
} from '@/src/components/common/wayd-grid-cell-renderers'
import {
  NavigationDto,
  StrategicInitiativeListDto,
} from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface StrategicInitiativesGridProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  gridHeight?: number | undefined
  viewSelector?: React.ReactNode | undefined
}

export interface NavigationLinkCellRendererProps {
  data: NavigationDto
}
export const StrategicInitiativeLinkCellRenderer = ({
  data,
}: NavigationLinkCellRendererProps) => {
  if (!data) return null
  return (
    <Link href={`/ppm/strategic-initiatives/${data.key}`}>{data.name}</Link>
  )
}

const StrategicInitiativesGrid: FC<StrategicInitiativesGridProps> = (
  props: StrategicInitiativesGridProps,
) => {
  const { refetch } = props

  const columnDefs = useMemo<ColDef<StrategicInitiativeListDto>[]>(() => [
    { field: 'key', width: 90 },
    {
      field: 'name',
      cellRenderer: StrategicInitiativeLinkCellRenderer,
      width: 300,
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
      cellRenderer: (params: ICellRendererParams<StrategicInitiativeListDto>) => {
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
      field: 'strategicInitiativeSponsors',
      headerName: 'Sponsors',
      valueGetter: (params) =>
        getSortedNames(params.data?.strategicInitiativeSponsors ?? []),
    },
    {
      field: 'strategicInitiativeOwners',
      headerName: 'Owners',
      valueGetter: (params) =>
        getSortedNames(params.data?.strategicInitiativeOwners ?? []),
    },
  ], [props.hidePortfolio])

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columnDefs={columnDefs}
      rowData={props.strategicInitiatives}
      loadData={refresh}
      loading={props.isLoading}
      toolbarActions={props.viewSelector}
      height={props.gridHeight}
      emptyMessage="No strategic initiatives found."
    />
  )
}

export default StrategicInitiativesGrid
