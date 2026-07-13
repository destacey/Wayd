'use client'

import {
  WaydGrid,
  createCsvColumn,
  renderPortfolioLink,
  renderProgramLink,
} from '@/src/components/common/wayd-grid'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import { ProgramListDto } from '@/src/services/wayd-api'
import { getSortedNameList } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import { FC } from 'react'

export interface ProgramsGridProps {
  programs: ProgramListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  gridHeight?: number | undefined
  viewSelector?: React.ReactNode | undefined
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const ProgramsGrid: FC<ProgramsGridProps> = (props: ProgramsGridProps) => {
  const { refetch } = props

  const columns: ColumnDef<ProgramListDto, any>[] = [
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
    // Context-redundant column: excluded from the defs (not meta.hide) so
    // it stays out of the column chooser and persisted layouts.
    ...(props.hidePortfolio
      ? []
      : [
          {
            id: 'portfolio',
            accessorKey: 'portfolio.name',
            header: 'Portfolio',
            size: 200,
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderPortfolioLink(row.original.portfolio),
          } satisfies ColumnDef<ProgramListDto, any>,
        ]),
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
    createCsvColumn<ProgramListDto>({
      id: 'programManagers',
      header: 'PMs',
      getValues: (row) => getSortedNameList(row.programManagers ?? []),
    }),
    createCsvColumn<ProgramListDto>({
      id: 'programOwners',
      header: 'Owners',
      getValues: (row) => getSortedNameList(row.programOwners ?? []),
    }),
    createCsvColumn<ProgramListDto>({
      id: 'programSponsors',
      header: 'Sponsors',
      getValues: (row) => getSortedNameList(row.programSponsors ?? []),
    }),
    createCsvColumn<ProgramListDto>({
      id: 'strategicThemes',
      header: 'Strategic Themes',
      getValues: (row) => getSortedNameList(row.strategicThemes ?? []),
    }),
  ]

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
      persistStateKey={props.persistStateKey}
      emptyMessage="No programs found."
    />
  )
}

export default ProgramsGrid
