'use client'

import { WaydGrid2, renderPortfolioLink } from '@/src/components/common/wayd-grid2'
import { ProjectPortfolioListDto } from '@/src/services/wayd-api'
import { getSortedNames } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import { ReactElement, useMemo } from 'react'

export interface PortfoliosGridProps {
  portfolios: ProjectPortfolioListDto[]
  viewSelector: ReactElement
  isLoading: boolean
  refetch: () => void
}

const PortfoliosGrid: React.FC<PortfoliosGridProps> = (
  props: PortfoliosGridProps,
) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<ProjectPortfolioListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderPortfolioLink(row.original),
      },
      {
        id: 'status',
        accessorKey: 'status.name',
        header: 'Status',
        meta: { filterType: 'set' },
      },
      {
        id: 'portfolioManagers',
        accessorFn: (row) => getSortedNames(row.portfolioManagers ?? []),
        header: 'PMs',
      },
      {
        id: 'portfolioOwners',
        accessorFn: (row) => getSortedNames(row.portfolioOwners ?? []),
        header: 'Owners',
      },
      {
        id: 'portfolioSponsors',
        accessorFn: (row) => getSortedNames(row.portfolioSponsors ?? []),
        header: 'Sponsors',
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
      data={props.portfolios}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="portfolios"
      rightSlot={props.viewSelector}
      emptyMessage="No portfolios found."
    />
  )
}

export default PortfoliosGrid
