'use client'

import { WaydGrid2, renderWorkspaceLink } from '@/src/components/common/wayd-grid2'
import { WorkspaceListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { ReactElement, useMemo } from 'react'

export interface WorkspacesGridProps {
  workspaces: WorkspaceListDto[]
  viewSelector: ReactElement
  isLoading: boolean
  refetch: () => void
}

const WorkspacesGrid = (props: WorkspacesGridProps) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<WorkspaceListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderWorkspaceLink(row.original),
      },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
      {
        id: 'ownership',
        accessorKey: 'ownership.name',
        header: 'Ownership',
        meta: { filterType: 'set' },
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
    <WaydGrid2
      columns={columns}
      data={props.workspaces ?? []}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="workspaces"
      rightSlot={props.viewSelector}
    />
  )
}

export default WorkspacesGrid
