'use client'

import EntityLink from '@/src/components/common/entity-link'
import {
  WaydGrid,
  createMultiValueSetFilter,
} from '@/src/components/common/wayd-grid'
import {
  renderProductLink,
  renderReleaseLink,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import { IncidentSummaryDto } from '@/src/services/wayd-api'
import { ReactNode } from 'react'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { Fragment } from 'react'
import { formatIncidentAge, instantToEpochMs } from './incident-format'
import { IncidentSeverityRank } from './incident-severity-rank'
import styles from './incidents.module.css'

const productNames = (row: IncidentSummaryDto) =>
  (row.products ?? []).map((product) => product.name)

const releaseNames = (row: IncidentSummaryDto) =>
  (row.releases ?? []).map((release) => release.name)

const columns: ColumnDef<IncidentSummaryDto, any>[] = [
  {
    id: 'key',
    accessorKey: 'key',
    header: 'Number',
    size: 100,
    cell: ({ row }) => (
      <Link href={`/product-management/incidents/${row.original.key}`}>
        {row.original.key}
      </Link>
    ),
  },
  {
    id: 'severity',
    accessorFn: (row) => row.severityLevel?.name ?? '',
    header: 'Severity',
    size: 120,
    meta: { filterType: 'set' },
    cell: ({ row }) => (
      <IncidentSeverityRank
        name={row.original.severityLevel.name}
        color={row.original.severityLevel.color}
        className={styles.listRank}
      />
    ),
  },
  {
    id: 'title',
    accessorKey: 'title',
    header: 'Title',
    size: 280,
    meta: { filterEnableSet: true },
    cell: ({ row }) => (
      <EntityLink href={`/product-management/incidents/${row.original.key}`}>
        {row.original.title}
      </EntityLink>
    ),
  },
  {
    id: 'products',
    accessorFn: (row) => productNames(row).join(', '),
    header: 'Products',
    size: 240,
    filterFn: createMultiValueSetFilter(productNames),
    meta: { filterType: 'set', multiValues: productNames },
    cell: ({ row }) =>
      (row.original.products ?? []).map((product, index) => (
        <Fragment key={product.id}>
          {index > 0 && ', '}
          {renderProductLink(product)}
        </Fragment>
      )),
  },
  {
    id: 'releases',
    accessorFn: (row) => releaseNames(row).join(', '),
    header: 'Releases',
    size: 240,
    filterFn: createMultiValueSetFilter(releaseNames),
    meta: { filterType: 'set', multiValues: releaseNames },
    cell: ({ row }) =>
      (row.original.releases ?? []).map((release, index) => (
        <Fragment key={release.id}>
          {index > 0 && ', '}
          {renderReleaseLink(release)}
        </Fragment>
      )),
  },
  {
    id: 'state',
    accessorFn: (row) => row.status?.name ?? '',
    header: 'State',
    size: 140,
    meta: { filterType: 'set' },
    cell: ({ row }) =>
      row.original.status ? (
        <Tooltip title={statusCategoryDescription(row.original.status)}>
          <span>
            <WorkflowStatusTag
              name={row.original.status.name}
              category={row.original.status.category}
            />
          </span>
        </Tooltip>
      ) : null,
  },
  {
    id: 'age',
    accessorFn: (row) => {
      const noticed = instantToEpochMs(row.noticedAt)
      return Number.isNaN(noticed) ? 0 : Date.now() - noticed
    },
    header: 'Age',
    size: 90,
    cell: ({ row }) => formatIncidentAge(row.original.noticedAt),
  },
  {
    id: 'reportedBy',
    accessorFn: (row) => row.reportedBy?.name ?? '',
    header: 'Reported by',
    size: 180,
    meta: { filterType: 'set' },
  },
  {
    id: 'commander',
    accessorFn: (row) => row.commander?.name ?? '',
    header: 'Commander',
    size: 180,
    meta: { filterType: 'set' },
  },
]

export interface IncidentsGridProps {
  incidents: IncidentSummaryDto[]
  isLoading: boolean
  refetch?: () => void
  rightSlot?: ReactNode
}

const IncidentsGrid = ({
  incidents,
  isLoading,
  refetch,
  rightSlot,
}: IncidentsGridProps) => (
  <WaydGrid
    columns={columns}
    data={incidents}
    isLoading={isLoading}
    onRefresh={refetch}
    csvFileName="incidents"
    persistStateKey="product-management-incidents"
    emptyMessage="No incidents yet."
    rightSlot={rightSlot}
  />
)

export default IncidentsGrid
