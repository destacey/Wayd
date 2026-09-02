'use client'

import { formatDateOnly, WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  renderPackageLink,
  renderProductLink,
  renderVersionLink,
} from '@/src/components/common/wayd-grid-core'
import { ReleaseDto } from '@/src/services/wayd-api'
import { Tag, Tooltip, Typography } from 'antd'

const { Text } = Typography

/**
 * One thing a release announces, whichever route it arrived by.
 *
 * The two routes are separate sets on the DTO, but they answer one question — what did this release
 * contain? — so they are flattened into one list and the route becomes a column. A reader asking that
 * question is not asking which of the two lists an item was written in.
 */
interface ContentsEntry {
  id: string
  route: 'Package' | 'Direct'
  /** The link cell, prebuilt: the two routes link to different pages. */
  item: React.ReactNode
  name: string
  detail: React.ReactNode
  releasedDate?: Date
}

const toEntries = (release: ReleaseDto): ContentsEntry[] => [
  ...(release.packages ?? []).map((entry) => ({
    id: entry.package.id,
    route: 'Package' as const,
    item: renderPackageLink(entry.package),
    name: entry.package.name,
    // A package's own contents live on its page; naming them here would duplicate a manifest that can
    // disagree with this copy.
    detail: <Text type="secondary">Package</Text>,
    releasedDate: entry.releasedDate,
  })),
  ...(release.versions ?? []).map((entry) => ({
    id: entry.version.id,
    route: 'Direct' as const,
    item: renderVersionLink(entry.version),
    name: entry.version.name,
    detail: entry.product ? (
      renderProductLink(entry.product)
    ) : (
      <Text type="secondary">—</Text>
    ),
    releasedDate: entry.releasedDate,
  })),
]

export interface ReleaseContentsProps {
  release: ReleaseDto
  isLoading?: boolean
}

const buildContentsColumns = (): ColumnDef<ContentsEntry, any>[] => [
  {
    id: 'route',
    accessorKey: 'route',
    header: 'Route',
    size: 110,
    meta: { filterType: 'set' },
    // The distinction is real and load-bearing — a version carried directly shipped on its own, where
    // nobody assembled a package — so it is stated rather than left to be inferred from the link.
    cell: ({ row }) =>
      row.original.route === 'Package' ? (
        <Tooltip title="Shipped inside a package, which is the deployment unit.">
          <Tag color="blue">Package</Tag>
        </Tooltip>
      ) : (
        <Tooltip title="Shipped on its own, with no package assembled.">
          <Tag>Direct</Tag>
        </Tooltip>
      ),
  },
  {
    id: 'item',
    accessorFn: (row) => row.name,
    header: 'Item',
    size: 220,
    meta: { filterEnableSet: true },
    cell: ({ row }) => row.original.item,
  },
  {
    id: 'detail',
    accessorFn: (row) => row.route,
    header: 'Product',
    size: 200,
    cell: ({ row }) => row.original.detail,
  },
  {
    id: 'releasedDate',
    accessorKey: 'releasedDate',
    header: 'Shipped',
    size: 150,
    meta: { columnType: 'dateOnly' },
    // Outstanding contents are what refuse an announcement, so an unshipped entry says so rather than
    // leaving an empty cell the reader has to interpret. The date is formatted here rather than left
    // to the column type, because an explicit cell replaces the type's own renderer.
    cell: ({ row }) =>
      row.original.releasedDate ? (
        formatDateOnly(row.original.releasedDate)
      ) : (
        <Text type="warning">Not yet shipped</Text>
      ),
  },
]

/**
 * Everything a release announces, in one list.
 *
 * Ordered packages first, since that is the usual route and the one carrying most of what shipped.
 */
const ReleaseContents = ({ release, isLoading }: ReleaseContentsProps) => (
  <WaydGrid
    columns={buildContentsColumns()}
    data={toEntries(release)}
    isLoading={isLoading}
    csvFileName={`release-${release.key}-contents`}
    emptyMessage="This release announces nothing. That is legitimate — a repackaging or a pricing change is announced with nothing deployed."
  />
)

export default ReleaseContents
