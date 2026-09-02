'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import { ReleasePackageDto } from '@/src/services/wayd-api'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { ReactElement } from 'react'

export interface ReleasePackagesGridProps {
  packages: ReleasePackageDto[]
  isLoading: boolean
  refetch?: () => void
  rightSlot?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  emptyMessage?: string
}

/**
 * The component count is derived here rather than read from the DTO, which carries no total — the
 * manifest is always projected in full, so counting it costs nothing and cannot disagree with the
 * lines the detail page shows.
 */
export const buildReleasePackageColumns = (): ColumnDef<
  ReleasePackageDto,
  any
>[] => [
  { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
  {
    id: 'version',
    accessorKey: 'version',
    header: 'Version',
    size: 160,
    meta: { filterEnableSet: true },
    // Linked by key rather than id, so the URL carries something a reader recognises.
    cell: ({ row }) => (
      <Link href={`/product-management/release-packages/${row.original.key}`}>
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
    id: 'componentCount',
    accessorFn: (row) => row.components?.length ?? 0,
    header: 'Components',
    size: 120,
  },
  {
    id: 'targetDate',
    accessorKey: 'targetDate',
    header: 'Target',
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

const ReleasePackagesGrid: React.FC<ReleasePackagesGridProps> = ({
  packages,
  isLoading,
  refetch,
  rightSlot,
  persistStateKey,
  emptyMessage = 'No release packages have been assembled.',
}) => (
  <WaydGrid
    columns={buildReleasePackageColumns()}
    data={packages}
    isLoading={isLoading}
    onRefresh={refetch}
    rightSlot={rightSlot}
    csvFileName="release-packages"
    persistStateKey={persistStateKey}
    emptyMessage={emptyMessage}
  />
)

export default ReleasePackagesGrid
