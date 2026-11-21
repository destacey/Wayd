'use client'

import { WaydGrid, formatDateTime } from '@/src/components/common/wayd-grid'
import { PendingTenantMigrationDto } from '@/src/services/api/wayd.schemas'
import { useGetPendingTenantMigrationsQuery } from '@/src/store/features/user-management/oidc-providers-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { instantTime } from '@/src/utils/instant'
import { Tag } from 'antd'
import Link from 'next/link'

/** Formats a value with a dash fallback when empty. */
const orDash = (value: unknown) => (value ? String(value) : '—')

const columns: ColumnDef<PendingTenantMigrationDto, any>[] = [
  {
    id: 'userName',
    accessorKey: 'userName',
    header: 'User',
    cell: ({ row }) => (
      <Link href={`/settings/user-management/users/${row.original.userId}`}>
        {row.original.userName}
      </Link>
    ),
  },
  {
    id: 'email',
    accessorKey: 'email',
    header: 'Email',
    size: 175,
    cell: ({ getValue }) => orDash(getValue()),
  },
  {
    id: 'sourceTenantId',
    accessorKey: 'sourceTenantId',
    header: 'Source tenant',
    size: 175,
    cell: ({ getValue }) => orDash(getValue()),
  },
  {
    id: 'targetTenantId',
    accessorKey: 'targetTenantId',
    header: 'Target tenant',
    size: 175,
  },
  {
    id: 'stagedAt',
    accessorKey: 'stagedAt',
    header: 'Staged',
    meta: { columnType: 'dateTime' },
    cell: ({ getValue }) => (getValue() ? formatDateTime(getValue()) : '—'),
  },
  {
    id: 'expiresAt',
    accessorKey: 'expiresAt',
    header: 'Expires',
    meta: { columnType: 'dateTime' },
    // Past it the user's sign-in no longer completes the migration; staging
    // them again restarts the 14 days.
    cell: ({ getValue }) => {
      const expiresAt = getValue()
      return expiresAt && instantTime(expiresAt) >= Date.now() ? (
        formatDateTime(expiresAt)
      ) : (
        <Tag color="warning">Expired</Tag>
      )
    },
  },
]

interface ActiveTenantMigrationsProps {
  providerId: string
}

const ActiveTenantMigrations = ({
  providerId,
}: ActiveTenantMigrationsProps) => {
  const { data, isLoading, refetch } =
    useGetPendingTenantMigrationsQuery(providerId)

  return (
    <WaydGrid
      columns={columns}
      data={data ?? []}
      isLoading={isLoading}
      onRefresh={() => {
        refetch()
      }}
      persistStateKey="settings-tenant-migrations"
      csvFileName="tenant-migrations"
      emptyMessage="No tenant migrations are currently in progress."
    />
  )
}

export default ActiveTenantMigrations
