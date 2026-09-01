'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { renderProductLink } from '@/src/components/common/wayd-grid-core'
import { ReleaseDto } from '@/src/services/wayd-api'
import Link from 'next/link'
import { ReactElement } from 'react'

export interface ReleasesGridProps {
  releases: ReleaseDto[]
  isLoading: boolean
  refetch: () => void
  rightSlot?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  /** Hidden where every row already shares one product — a release detail page's siblings. */
  showProduct?: boolean
}

/**
 * The package a release shipped in is deliberately absent. `Release.PackageId` is never written, so
 * the column would be empty on every row; a package's membership lives in its manifest.
 */
const buildColumns = (showProduct: boolean): ColumnDef<ReleaseDto, any>[] => [
  { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
  {
    id: 'version',
    accessorKey: 'version',
    header: 'Version',
    size: 160,
    meta: { filterEnableSet: true },
    // Linked by key rather than id, so the URL carries something a reader recognises.
    cell: ({ row }) => (
      <Link href={`/delivery/releases/${row.original.key}`}>
        {row.original.version}
      </Link>
    ),
  },
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
    size: 220,
    meta: { filterEnableSet: true },
  },
  ...(showProduct
    ? [
        {
          id: 'product',
          accessorFn: (row: ReleaseDto) => row.product?.name ?? '',
          header: 'Product',
          size: 200,
          meta: { filterType: 'set' as const },
          cell: ({ row }: { row: { original: ReleaseDto } }) =>
            renderProductLink(row.original.product),
        } as ColumnDef<ReleaseDto, any>,
      ]
    : []),
  {
    id: 'status',
    accessorFn: (row) => row.status?.name ?? '',
    header: 'Status',
    size: 130,
    meta: { filterType: 'set' },
  },
  {
    id: 'targetDate',
    accessorKey: 'targetDate',
    header: 'Target',
    size: 130,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'cutDate',
    accessorKey: 'cutDate',
    header: 'Cut',
    size: 130,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'releasedDate',
    accessorKey: 'releasedDate',
    header: 'Released',
    size: 130,
    meta: { columnType: 'dateOnly' },
  },
]

const ReleasesGrid: React.FC<ReleasesGridProps> = ({
  releases,
  isLoading,
  refetch,
  rightSlot,
  persistStateKey,
  showProduct = true,
}) => (
  <WaydGrid
    columns={buildColumns(showProduct)}
    data={releases}
    isLoading={isLoading}
    onRefresh={refetch}
    rightSlot={rightSlot}
    csvFileName="releases"
    persistStateKey={persistStateKey}
    emptyMessage="No releases have been planned."
  />
)

export default ReleasesGrid
