import { FC, useMemo } from 'react'
import { WaydGrid, renderTeamLink } from '../wayd-grid'
import { PlanningIntervalTeamResponse } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'

export interface TeamsGridProps {
  teams: PlanningIntervalTeamResponse[]
  isLoading: boolean
  refetch: () => void
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const TeamsGrid: FC<TeamsGridProps> = (props) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<PlanningIntervalTeamResponse, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original),
      },
      { id: 'code', accessorKey: 'code', header: 'Code', size: 125 },
      {
        id: 'type',
        accessorKey: 'type',
        header: 'Type',
        meta: { filterType: 'set' },
      },
      {
        id: 'teamOfTeams',
        accessorKey: 'teamOfTeams.name',
        header: 'Team of Teams',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.teamOfTeams),
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        meta: { columnType: 'yesNo' },
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.teams}
      onRefresh={refresh}
      isLoading={props.isLoading}
      persistStateKey={props.persistStateKey}
      csvFileName="teams"
    />
  )
}

export default TeamsGrid
