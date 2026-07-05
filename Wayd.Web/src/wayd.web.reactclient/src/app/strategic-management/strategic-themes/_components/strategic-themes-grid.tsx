'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import { StrategicThemeListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import { useMemo } from 'react'

export interface StrategicThemesGridProps {
  strategicThemesData: StrategicThemeListDto[]
  strategicThemesLoading: boolean
  refreshStrategicThemes: () => void
  gridHeight?: number | undefined
}

const StrategicThemesGrid: React.FC<StrategicThemesGridProps> = (
  props: StrategicThemesGridProps,
) => {
  const columns = useMemo<ColumnDef<StrategicThemeListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 350,
        cell: ({ row }) => (
          <Link
            href={`/strategic-management/strategic-themes/${row.original.key}`}
          >
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 125,
        meta: { filterType: 'set' },
      },
    ],
    [],
  )

  return (
    <WaydGrid
      height={props.gridHeight}
      columns={columns}
      data={props.strategicThemesData}
      isLoading={props.strategicThemesLoading}
      onRefresh={props.refreshStrategicThemes}
      persistStateKey="strategic-themes"
      csvFileName="strategic-themes"
    />
  )
}

export default StrategicThemesGrid
