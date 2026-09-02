'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  renderPackageLink,
  renderVersionLink,
} from '@/src/components/common/wayd-grid-core'
import {
  statusCategoryDescription,
  WorkflowStatusTag,
} from '@/src/components/common/status-workflows'
import { DeploymentDto } from '@/src/services/wayd-api'
import { Tooltip } from 'antd'
import Link from 'next/link'
import { ReactElement } from 'react'

export interface DeploymentsGridProps {
  deployments: DeploymentDto[]
  isLoading: boolean
  refetch?: () => void
  rightSlot?: ReactElement
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
  emptyMessage?: string
}

/**
 * The deployment record.
 *
 * The environment category shown is the one frozen on the deployment, not the environment's current
 * one. Reclassifying an environment must not rewrite what past deployments counted as, so a row can
 * legitimately disagree with the environment's category today.
 *
 * A deployment carries either a release or a package, never both, so the two share one column — a
 * reader wants to know what shipped, not which of two possible fields recorded it.
 */
export const buildDeploymentColumns = (): ColumnDef<DeploymentDto, any>[] => [
  {
    id: 'key',
    accessorKey: 'key',
    header: 'Key',
    size: 90,
    // A deployment has no name of its own, so its key is what carries the link — unlike a release,
    // which is identified by its version.
    cell: ({ row }) => (
      <Link href={`/delivery/deployments/${row.original.key}`}>
        {row.original.key}
      </Link>
    ),
  },
  {
    id: 'deployed',
    accessorFn: (row) => row.version?.name ?? row.package?.name ?? '',
    header: 'Deployed',
    size: 180,
    meta: { filterEnableSet: true },
    cell: ({ row }) =>
      row.original.version
        ? renderVersionLink(row.original.version)
        : renderPackageLink(row.original.package),
  },
  {
    id: 'deployedKind',
    accessorFn: (row) => (row.version ? 'Version' : 'Package'),
    header: 'Kind',
    size: 110,
    meta: { filterType: 'set' },
  },
  {
    id: 'environment',
    accessorFn: (row) => row.environment?.name ?? '',
    header: 'Environment',
    size: 170,
    meta: { filterType: 'set' },
  },
  {
    id: 'environmentCategory',
    accessorKey: 'environmentCategory',
    header: 'Category',
    size: 130,
    meta: { filterType: 'set' },
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
    id: 'startedAt',
    accessorKey: 'startedAt',
    header: 'Started',
    size: 170,
    meta: { columnType: 'dateTime' },
  },
  {
    id: 'completedAt',
    accessorKey: 'completedAt',
    header: 'Completed',
    size: 170,
    meta: { columnType: 'dateTime' },
  },
  {
    id: 'artifactId',
    accessorKey: 'artifactId',
    header: 'Artifact',
    size: 160,
  },
]

const DeploymentsGrid: React.FC<DeploymentsGridProps> = ({
  deployments,
  isLoading,
  refetch,
  rightSlot,
  persistStateKey,
  emptyMessage = 'No deployments have been recorded.',
}) => (
  <WaydGrid
    columns={buildDeploymentColumns()}
    data={deployments}
    isLoading={isLoading}
    onRefresh={refetch}
    rightSlot={rightSlot}
    csvFileName="deployments"
    persistStateKey={persistStateKey}
    emptyMessage={emptyMessage}
  />
)

export default DeploymentsGrid
