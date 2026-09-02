'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { renderProductLink } from '@/src/components/common/wayd-grid-core'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import { VersionDto } from '@/src/services/wayd-api'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { ReactElement } from 'react'

export interface VersionsGridProps {
  versions: VersionDto[]
  isLoading: boolean
  refetch: () => void
  rightSlot?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  /** Hidden where every row already shares one product — a version detail page's siblings. */
  showProduct?: boolean
}

/**
 * The package a version shipped in is deliberately absent. `Version.PackageId` is never written, so
 * the column would be empty on every row; a package's membership lives in its manifest.
 */
export const buildReleaseColumns = (showProduct: boolean): ColumnDef<VersionDto, any>[] => [
  { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
  // Ahead of the version: 4.8.2 and 2026.04 say nothing side by side without their products.
  ...(showProduct
    ? [
        {
          id: 'product',
          accessorFn: (row: VersionDto) => row.product?.name ?? '',
          header: 'Product',
          size: 200,
          meta: { filterType: 'set' as const },
          cell: ({ row }: { row: { original: VersionDto } }) =>
            renderProductLink(row.original.product),
        } as ColumnDef<VersionDto, any>,
      ]
    : []),
  {
    id: 'version',
    accessorKey: 'version',
    header: 'Version',
    size: 160,
    meta: { filterEnableSet: true },
    // Linked by key rather than id, so the URL carries something a reader recognises.
    cell: ({ row }) => (
      <Link href={`/delivery/versions/${row.original.key}`}>
        {row.original.number}
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
  {
    id: 'status',
    accessorFn: (row) => row.status?.name ?? '',
    header: 'Status',
    size: 130,
    meta: { filterType: 'set' },
    // A status name is the workflow's own word and can be renamed to anything. The tooltip carries
    // the category, which is the fixed meaning rollups and filters group on.
    cell: ({ row }) => (
      <Tooltip title={statusCategoryDescription(row.original.status.category)}>
        <span>
          <WorkflowStatusTag
            name={row.original.status.name}
            category={row.original.status.category}
          />
        </span>
      </Tooltip>
    ),
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

const VersionsGrid: React.FC<VersionsGridProps> = ({
  versions,
  isLoading,
  refetch,
  rightSlot,
  persistStateKey,
  showProduct = true,
}) => (
  <WaydGrid
    columns={buildReleaseColumns(showProduct)}
    data={versions}
    isLoading={isLoading}
    onRefresh={refetch}
    rightSlot={rightSlot}
    csvFileName="versions"
    persistStateKey={persistStateKey}
    emptyMessage="No versions have been planned."
  />
)

export default VersionsGrid
