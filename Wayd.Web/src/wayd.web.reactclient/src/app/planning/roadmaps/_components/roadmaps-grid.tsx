'use client'

import { WaydGrid2 } from '@/src/components/common/wayd-grid2'
import { RoadmapListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import { FC, ReactNode, useMemo } from 'react'

export interface RoadmapsGridProps {
  roadmapsData: RoadmapListDto[]
  roadmapsLoading: boolean
  refreshRoadmaps: () => void
  gridHeight?: number | undefined
  viewSelector?: ReactNode | undefined
  parentRoadmapId?: string | undefined
}

const RoadmapsGrid: FC<RoadmapsGridProps> = (props: RoadmapsGridProps) => {
  const columns = useMemo<ColumnDef<RoadmapListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        meta: { filterEnableSet: true },
        cell: ({ row }) => (
          <Link href={`/planning/roadmaps/${row.original.key}`}>
            {row.original.name}
          </Link>
        ),
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
      {
        id: 'roadmapManagers',
        accessorFn: (row) => getSortedNames(row.roadmapManagers ?? []),
        header: 'Roadmap Managers',
      },
      {
        id: 'visibility',
        accessorKey: 'visibility.name',
        header: 'Visibility',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 120,
        meta: { filterType: 'set' },
      },
    ],
    [],
  )

  return (
    <WaydGrid2
      height={props.gridHeight ?? 650}
      columns={columns}
      data={props.roadmapsData}
      onRefresh={props.refreshRoadmaps}
      isLoading={props.roadmapsLoading}
      csvFileName="roadmaps"
      rightSlot={props.viewSelector}
    />
  )
}

export default RoadmapsGrid
