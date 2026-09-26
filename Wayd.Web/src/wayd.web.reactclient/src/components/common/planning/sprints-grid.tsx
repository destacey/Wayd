'use client'

import {
  WaydGrid,
  renderSprintLink,
  renderTeamLink,
} from '@/src/components/common/wayd-grid'
import { SprintListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '../wayd-grid-core'
import { FC, useMemo } from 'react'

export interface SprintsGridProps {
  sprints: SprintListDto[]
  isLoading: boolean
  refetch: () => void
  hideTeam?: boolean
  gridHeight?: number | undefined
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

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
      // Context-redundant column: excluded from the defs (not meta.unavailable) so
      // it stays out of the column chooser and persisted layouts.
      ...(props.hideTeam
        ? []
        : [
            {
              id: 'team',
              accessorKey: 'team.name',
              header: 'Team',
              size: 200,
              meta: { filterEnableSet: true },
              cell: ({ row }) => renderTeamLink(row.original.team),
            } satisfies ColumnDef<SprintListDto, any>,
          ]),
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
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        size: 150,
        meta: { columnType: 'dateOnly' },
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
      persistStateKey={props.persistStateKey}
      csvFileName="sprints"
      emptyMessage="No sprints found."
    />
  )
}

export default SprintsGrid
