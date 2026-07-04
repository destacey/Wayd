'use client'

import {
  WaydGrid,
  renderPortfolioLink,
  renderProgramLink,
} from '@/src/components/common/wayd-grid'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import { ProgramListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
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

  const columns = useMemo<ColumnDef<ProgramListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderProgramLink(row.original),
      },
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
        cell: ({ row }) =>
          row.original.status ? (
            <LifecycleStatusTag status={row.original.status} />
          ) : null,
      },
      {
        id: 'portfolio',
        accessorKey: 'portfolio.name',
        header: 'Portfolio',
        size: 200,
        meta: { hide: props.hidePortfolio, filterEnableSet: true },
        cell: ({ row }) => renderPortfolioLink(row.original.portfolio),
      },
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start',
        size: 125,
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        size: 125,
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'programManagers',
        accessorFn: (row) => getSortedNames(row.programManagers ?? []),
        header: 'PMs',
      },
      {
        id: 'programOwners',
        accessorFn: (row) => getSortedNames(row.programOwners ?? []),
        header: 'Owners',
      },
      {
        id: 'programSponsors',
        accessorFn: (row) => getSortedNames(row.programSponsors ?? []),
        header: 'Sponsors',
      },
      {
        id: 'strategicThemes',
        accessorFn: (row) => getSortedNames(row.strategicThemes ?? []),
        header: 'Strategic Themes',
      },
    ],
    [props.hidePortfolio],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.programs}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="programs"
      rightSlot={props.viewSelector}
      height={props.gridHeight}
      emptyMessage="No programs found."
    />
  )
}

export default ProgramsGrid
