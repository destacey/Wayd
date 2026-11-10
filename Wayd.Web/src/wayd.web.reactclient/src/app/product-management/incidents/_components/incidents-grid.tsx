'use client'

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
import { Tooltip } from 'antd'
import Link from 'next/link'
import { Fragment } from 'react'
import { formatIncidentAge } from './incident-format'
import { IncidentSeverityRank } from './incident-severity-rank'
import styles from './incidents.module.css'

const productNames = (row: IncidentSummaryDto) =>
  (row.products ?? []).map((product) => product.name)

const releaseNames = (row: IncidentSummaryDto) =>
  (row.releases ?? []).map((release) => release.name)

const columns: ColumnDef<IncidentSummaryDto, any>[] = [
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
      <Link href={`/product-management/incidents/${row.original.key}`}>
        {row.original.title}
      </Link>
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
    accessorFn: (row) => formatIncidentAge(row.noticedAt),
    header: 'Age',
    size: 90,
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
}

const IncidentsGrid = ({
  incidents,
  isLoading,
  refetch,
}: IncidentsGridProps) => (
  <WaydGrid
    columns={columns}
    data={incidents}
    isLoading={isLoading}
    onRefresh={refetch}
    csvFileName="incidents"
    persistStateKey="product-management-incidents"
    emptyMessage="No incidents yet."
  />
)

export default IncidentsGrid
