'use client'

import EntityLink from '@/src/components/common/entity-link'
import {
  WaydGrid,
  type FilterOptionsSource,
  type ServerPageRequest,
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
import { incidentsGetIncidents } from '@/src/services/api/incidents'
import {
  IncidentSummaryDto,
  ProductStatusAlias,
} from '@/src/services/api/wayd.schemas'
import {
  useIncidentFilterOptions,
  type IncidentFilterField,
} from '@/src/store/features/product-management/incidents-api'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { Fragment, ReactNode } from 'react'
import { formatIncidentAge, instantToEpochMs } from './incident-format'
import { IncidentSeverityRank } from './incident-severity-rank'
import styles from './incidents.module.css'

/**
 * The fields `GetIncidentsQuery.Fields` filters and sorts by, named as the
 * columns below. `products` and `releases` filter but do not sort, so their
 * columns turn sorting off themselves.
 */
export const INCIDENT_FIELDS = [
  'key',
  'title',
  'severity',
  'state',
  'products',
  'releases',
  'reportedBy',
  'commander',
  'noticedAt',
] as const

const productNames = (row: IncidentSummaryDto) =>
  (row.products ?? []).map((product) => product.name)

const releaseNames = (row: IncidentSummaryDto) =>
  (row.releases ?? []).map((release) => release.name)

type SourceFor = (
  field: IncidentFilterField,
  extra?: Omit<FilterOptionsSource, 'load'>,
) => FilterOptionsSource

const columnsWith = (
  sourceFor: SourceFor,
): ColumnDef<IncidentSummaryDto, any>[] => [
  {
    id: 'key',
    accessorKey: 'key',
    header: 'Number',
    size: 100,
    meta: { filterType: 'number' },
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
    meta: { filterType: 'set', filterOptionsSource: sourceFor('severity') },
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
    enableSorting: false,
    meta: { filterType: 'set', filterOptionsSource: sourceFor('products') },
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
    enableSorting: false,
    meta: { filterType: 'set', filterOptionsSource: sourceFor('releases') },
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
    meta: { filterType: 'set', filterOptionsSource: sourceFor('state') },
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
    // Shown, not sorted or filtered: the server orders by when an incident was
    // noticed, which the Noticed column offers.
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
    id: 'noticedAt',
    accessorKey: 'noticedAt',
    header: 'Noticed',
    size: 170,
    meta: { columnType: 'dateTime' },
  },
  {
    id: 'reportedBy',
    accessorFn: (row) => row.reportedBy?.name ?? '',
    header: 'Reported by',
    size: 180,
    meta: { filterType: 'set', filterOptionsSource: sourceFor('reportedBy') },
  },
  {
    id: 'commander',
    accessorFn: (row) => row.commander?.name ?? '',
    header: 'Commander',
    size: 180,
    meta: {
      filterType: 'set',
      filterOptionsSource: sourceFor('commander', { includeBlank: true }),
    },
  },
]

export interface IncidentsGridProps {
  /** The filter bar's states; the grid's own filters narrow within them. */
  statuses: ProductStatusAlias[]
  /** The filter bar's severity levels. */
  severityLevelIds: string[]
  /** A value whose change refetches the rows in view (after an incident is opened, say). */
  refreshKey?: unknown
  /** Called by the toolbar's refresh, beside refetching the rows. */
  onRefresh?: () => void
  rightSlot?: ReactNode
}

/**
 * Every incident matching the filter bar, as one list the server pages, sorts
 * and filters as the user scrolls.
 */
const IncidentsGrid = ({
  statuses,
  severityLevelIds,
  refreshKey,
  onRefresh,
  rightSlot,
}: IncidentsGridProps) => {
  const sourceFor = useIncidentFilterOptions()

  const fetchPage = async ({
    page,
    pageSize,
    filter,
    sort,
    signal,
  }: ServerPageRequest) => {
    const result = await incidentsGetIncidents(
      {
        page,
        pageSize,
        filter,
        sort,
        status: statuses.length > 0 ? statuses : undefined,
        severityLevelId:
          severityLevelIds.length > 0 ? severityLevelIds : undefined,
      },
      { signal },
    )
    return { items: result.items, totalCount: result.totalCount }
  }

  return (
    <WaydGrid
      columns={columnsWith(sourceFor)}
      server={{
        fetchPage,
        listKey: [statuses, severityLevelIds],
        fields: INCIDENT_FIELDS,
        noun: 'incidents',
        refreshKey,
      }}
      onRefresh={onRefresh}
      csvFileName="incidents"
      persistStateKey="product-management-incidents"
      emptyMessage="No incidents yet."
      rightSlot={rightSlot}
    />
  )
}

export default IncidentsGrid
