'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { renderProductLink } from '@/src/components/common/wayd-grid-core'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import { ReleaseDto } from '@/src/services/wayd-api'
import { Tooltip, Typography } from 'antd'
import Link from 'next/link'
import { ReactElement } from 'react'

const { Text } = Typography

export interface ReleasesGridProps {
  releases: ReleaseDto[]
  isLoading: boolean
  refetch?: () => void
  rightSlot?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  /** Hidden where every row already shares one product — a product detail page's releases. */
  showProduct?: boolean
  emptyMessage?: string
}

/**
 * The contents count is derived here rather than read from the DTO, which carries no total — both
 * sets are always projected in full, so counting them costs nothing and cannot disagree with what the
 * detail page lists.
 */
const contentsSummary = (release: ReleaseDto): string => {
  const packages = release.packages?.length ?? 0
  const versions = release.versions?.length ?? 0

  if (packages === 0 && versions === 0) {
    // Not "None": an empty release is a legitimate announcement rather than an omission, and a blank
    // cell reads as missing data.
    return 'Empty'
  }

  const parts: string[] = []
  if (packages > 0) parts.push(`${packages} pkg`)
  if (versions > 0) parts.push(`${versions} ver`)

  return parts.join(' · ')
}

export const buildReleaseColumns = (
  showProduct: boolean,
): ColumnDef<ReleaseDto, any>[] => [
  { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
  ...(showProduct
    ? [
        {
          id: 'product',
          accessorFn: (row: ReleaseDto) => row.product?.name ?? '',
          header: 'Product',
          size: 200,
          meta: { filterType: 'set' as const },
          // A release spanning product lines has none, and that is a fact about it rather than a gap.
          cell: ({ row }: { row: { original: ReleaseDto } }) =>
            row.original.product ? (
              renderProductLink(row.original.product)
            ) : (
              <Text type="secondary">Spans product lines</Text>
            ),
        } as ColumnDef<ReleaseDto, any>,
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
      <Link href={`/product-management/releases/${row.original.key}`}>
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
    id: 'targetDate',
    accessorKey: 'targetDate',
    header: 'Target',
    size: 130,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'releasedDate',
    accessorKey: 'releasedDate',
    header: 'Announced',
    size: 130,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'contents',
    accessorFn: (row) => contentsSummary(row),
    header: 'Contents',
    size: 120,
  },
]

const ReleasesGrid: React.FC<ReleasesGridProps> = ({
  releases,
  isLoading,
  refetch,
  rightSlot,
  persistStateKey,
  showProduct = true,
  emptyMessage = 'No releases have been planned.',
}) => (
  <WaydGrid
    columns={buildReleaseColumns(showProduct)}
    data={releases}
    isLoading={isLoading}
    onRefresh={refetch}
    rightSlot={rightSlot}
    csvFileName="releases"
    persistStateKey={persistStateKey}
    emptyMessage={emptyMessage}
  />
)

export default ReleasesGrid
