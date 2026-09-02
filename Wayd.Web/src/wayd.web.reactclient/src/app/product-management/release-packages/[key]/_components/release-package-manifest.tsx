'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  renderProductLink,
  renderVersionLink,
} from '@/src/components/common/wayd-grid-core'
import {
  ManifestEntryKind,
  ReleasePackageComponentDto,
} from '@/src/services/wayd-api'
import { Tag, Tooltip } from 'antd'

export interface ReleasePackageManifestProps {
  components: ReleasePackageComponentDto[]
  isLoading?: boolean
}

const kindLabel: Record<ManifestEntryKind, string> = {
  [ManifestEntryKind.Changed]: 'Changed',
  [ManifestEntryKind.CarriedForward]: 'Carried Forward',
}

const kindDescription: Record<ManifestEntryKind, string> = {
  [ManifestEntryKind.Changed]: 'This component changed in this package.',
  [ManifestEntryKind.CarriedForward]:
    'This component shipped unchanged, recorded so the manifest says what was in the box.',
}

/**
 * A package's manifest.
 *
 * A component's release is a link only where one was recorded — a carried-forward line often names a
 * version that was never cut as a release in Wayd, which is why the manifest holds the version as
 * text of its own rather than reading it through the release.
 */
export const buildManifestColumns = (): ColumnDef<
  ReleasePackageComponentDto,
  any
>[] => [
  {
    id: 'product',
    accessorFn: (row) => row.product?.name ?? '',
    header: 'Component',
    size: 220,
    meta: { filterType: 'set' },
    cell: ({ row }) => renderProductLink(row.original.product),
  },
  // Two columns, because a manifest line holds two different things. The string is what the package
  // recorded shipping; the record is the version in Wayd it points at, where it points at one. They
  // are usually equal and deliberately not the same field — a carried-forward line often names a
  // version that was never cut here, which is why the link is nullable and the string is not.
  {
    id: 'version',
    accessorKey: 'version',
    header: 'Version',
    size: 160,
    meta: { filterEnableSet: true },
  },
  {
    id: 'versionRecord',
    accessorFn: (row) => row.versionRecord?.name ?? '',
    header: 'Version Record',
    size: 160,
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderVersionLink(row.original.versionRecord),
  },
  {
    id: 'kind',
    accessorFn: (row) => kindLabel[row.kind],
    header: 'Kind',
    size: 150,
    meta: { filterType: 'set' },
    cell: ({ row }) => (
      <Tooltip title={kindDescription[row.original.kind]}>
        <Tag
          color={
            row.original.kind === ManifestEntryKind.Changed ? 'blue' : 'default'
          }
        >
          {kindLabel[row.original.kind]}
        </Tag>
      </Tooltip>
    ),
  },
]

const ReleasePackageManifest = ({
  components,
  isLoading,
}: ReleasePackageManifestProps) => (
  <WaydGrid
    columns={buildManifestColumns()}
    data={components}
    isLoading={isLoading}
    csvFileName="release-package-manifest"
    emptyMessage="This package has no components."
  />
)

export default ReleasePackageManifest
