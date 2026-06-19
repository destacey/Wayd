'use client'

import { WaydGrid } from '@/src/components/common'
import { renderTeamLinkHelper } from '@/src/components/common/wayd-grid-cell-renderers'
import { SprintListDto } from '@/src/services/wayd-api'
import {
  ColDef,
  ICellRendererParams,
  ValueFormatterParams,
} from 'ag-grid-community'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import Link from 'next/link'
import { FC, useMemo } from 'react'

dayjs.extend(utc)

export interface SprintsGridProps {
  sprints: SprintListDto[]
  isLoading: boolean
  refetch: () => void
  hideTeam?: boolean
  gridHeight?: number | undefined
}

const sprintLinkCellRenderer = (params: ICellRendererParams<SprintListDto>) => {
  if (!params.data) return null
  return <Link href={`/planning/sprints/${params.data.key}`}>{params.value}</Link>
}

const teamCellRenderer = (params: ICellRendererParams<SprintListDto>) =>
  renderTeamLinkHelper(params.data?.team)

const formatUtcCalendarDate = (value: unknown) =>
  value ? dayjs.utc(value as Date).format('MMM D, YYYY') : ''

const utcAsCalendarDateValueFormatter = (
  params: ValueFormatterParams<SprintListDto>,
) => formatUtcCalendarDate(params.value)

const SprintsGrid: FC<SprintsGridProps> = (props: SprintsGridProps) => {
  const { refetch, sprints = [] } = props

  const columnDefs = useMemo<ColDef<SprintListDto>[]>(() => [
    { field: 'key', width: 90 },
    { field: 'name', width: 250, cellRenderer: sprintLinkCellRenderer },
    {
      field: 'team.name',
      headerName: 'Team',
      width: 200,
      hide: props.hideTeam,
      cellRenderer: teamCellRenderer,
    },
    { field: 'state.name', headerName: 'State', width: 125 },
    {
      field: 'start',
      headerName: 'Start',
      width: 150,
      sort: 'desc',
      cellDataType: 'date',
      filterParams: {
        includeTime: false,
      },
      valueFormatter: utcAsCalendarDateValueFormatter,
      getQuickFilterText: (params) => formatUtcCalendarDate(params.value),
    },
    {
      field: 'end',
      headerName: 'End',
      width: 150,
      cellDataType: 'date',
      filterParams: {
        includeTime: false,
      },
      valueFormatter: utcAsCalendarDateValueFormatter,
      getQuickFilterText: (params) => formatUtcCalendarDate(params.value),
    },
  ], [props.hideTeam])

  return (
    <WaydGrid
      columnDefs={columnDefs}
      rowData={sprints}
      loadData={refetch}
      loading={props.isLoading}
      height={props.gridHeight ?? 650}
      emptyMessage="No sprints found."
    />
  )
}

export default SprintsGrid
