'use client'

import { WaydGrid2, formatDateTime } from '@/src/components/common/wayd-grid2'
import { PendingTenantMigrationDto } from '@/src/services/wayd-api'
import { useGetPendingTenantMigrationsQuery } from '@/src/store/features/user-management/oidc-providers-api'
import type { ColumnDef } from '@tanstack/react-table'
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
    <WaydGrid2
      columns={columns}
      data={data ?? []}
      isLoading={isLoading}
      onRefresh={() => {
        refetch()
      }}
      csvFileName="tenant-migrations"
      emptyMessage="No tenant migrations are currently in progress."
    />
  )
}

export default ActiveTenantMigrations
