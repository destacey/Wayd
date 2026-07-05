'use client'

import {
  WaydGrid,
  renderPortfolioLink,
} from '@/src/components/common/wayd-grid'
import LifecycleStatusTag from '@/src/components/common/lifecycle-status-tag'
import {
  NavigationDto,
  StrategicInitiativeListDto,
} from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface StrategicInitiativesGridProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  gridHeight?: number | undefined
  viewSelector?: React.ReactNode | undefined
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

/** Renders a strategic initiative as a link to its page. */
export const renderStrategicInitiativeLink = (
  initiative: NavigationDto | null | undefined,
) => {
  if (!initiative) return null
  return (
    <Link href={`/ppm/strategic-initiatives/${initiative.key}`}>
      {initiative.name}
    </Link>
  )
}

const StrategicInitiativesGrid: FC<StrategicInitiativesGridProps> = (
  props: StrategicInitiativesGridProps,
) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<StrategicInitiativeListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderStrategicInitiativeLink(row.original),
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
            } satisfies ColumnDef<StrategicInitiativeListDto, any>,
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
      {
        id: 'strategicInitiativeSponsors',
        accessorFn: (row) =>
          getSortedNames(row.strategicInitiativeSponsors ?? []),
        header: 'Sponsors',
      },
      {
        id: 'strategicInitiativeOwners',
        accessorFn: (row) =>
          getSortedNames(row.strategicInitiativeOwners ?? []),
        header: 'Owners',
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
      data={props.strategicInitiatives}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="strategic-initiatives"
      rightSlot={props.viewSelector}
      height={props.gridHeight}
      persistStateKey={props.persistStateKey}
      emptyMessage="No strategic initiatives found."
    />
  )
}

export default StrategicInitiativesGrid
