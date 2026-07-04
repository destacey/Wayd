'use client'

import {
  WaydGrid,
  renderSprintLink,
  renderTeamLink,
} from '@/src/components/common/wayd-grid'
import { SprintListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import { FC, useMemo } from 'react'

dayjs.extend(utc)

export interface SprintsGridProps {
  sprints: SprintListDto[]
  isLoading: boolean
  refetch: () => void
  hideTeam?: boolean
  gridHeight?: number | undefined
}

/**
 * Sprint start/end are stored as UTC calendar dates; formatting them with the
 * local-timezone `dateOnly` column type would shift them by a day. Render via
 * `dayjs.utc` so the calendar date is preserved. The raw value still flows to
 * the date filter/sort via `meta.columnType: 'dateOnly'`.
 */
const formatUtcCalendarDate = (value: unknown) =>
  value ? dayjs.utc(value as Date).format('MMM D, YYYY') : ''

const defaultSorting = [{ id: 'start', desc: true }]

const SprintsGrid: FC<SprintsGridProps> = (props: SprintsGridProps) => {
  const { refetch, sprints = [] } = props

  const columns = useMemo<ColumnDef<SprintListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 250,
        meta: { filterEnableSet: true },
        cell: ({ row }) =>
          renderSprintLink(row.original, { showTeamCode: false }),
      },
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        size: 200,
        meta: { hide: props.hideTeam, filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start',
        size: 150,
        meta: { columnType: 'dateOnly' },
        cell: ({ getValue }) => formatUtcCalendarDate(getValue()),
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        size: 150,
        meta: { columnType: 'dateOnly' },
        cell: ({ getValue }) => formatUtcCalendarDate(getValue()),
      },
    ],
    [props.hideTeam],
  )

  return (
    <WaydGrid
      columns={columns}
      data={sprints}
      onRefresh={refetch}
      isLoading={props.isLoading}
      height={props.gridHeight}
      initialSorting={defaultSorting}
      csvFileName="sprints"
      emptyMessage="No sprints found."
    />
  )
}

export default SprintsGrid
