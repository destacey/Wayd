'use client'

import {
  WaydGrid,
  createCsvColumn,
  renderPortfolioLink,
} from '@/src/components/common/wayd-grid'
import { ProjectPortfolioListDto } from '@/src/services/wayd-api'
import { getSortedNameList } from '@/src/utils'
import type { ColumnDef } from '@tanstack/react-table'
import { ReactElement } from 'react'

export interface PortfoliosGridProps {
  portfolios: ProjectPortfolioListDto[]
  viewSelector: ReactElement
  isLoading: boolean
  refetch: () => void
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const PortfoliosGrid: React.FC<PortfoliosGridProps> = (
  props: PortfoliosGridProps,
) => {
  const { refetch } = props

  const columns: ColumnDef<ProjectPortfolioListDto, any>[] = [
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
    createCsvColumn<ProjectPortfolioListDto>({
      id: 'portfolioManagers',
      header: 'PMs',
      getValues: (row) => getSortedNameList(row.portfolioManagers ?? []),
    }),
    createCsvColumn<ProjectPortfolioListDto>({
      id: 'portfolioOwners',
      header: 'Owners',
      getValues: (row) => getSortedNameList(row.portfolioOwners ?? []),
    }),
    createCsvColumn<ProjectPortfolioListDto>({
      id: 'portfolioSponsors',
      header: 'Sponsors',
      getValues: (row) => getSortedNameList(row.portfolioSponsors ?? []),
    }),
  ]

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.portfolios}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="portfolios"
      rightSlot={props.viewSelector}
      persistStateKey={props.persistStateKey}
      emptyMessage="No portfolios found."
    />
  )
}

export default PortfoliosGrid
