'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { UserIdentityDto } from '@/src/services/wayd-api'
import { useGetUserIdentityHistoryQuery } from '@/src/store/features/user-management/users-api'
import { Alert, Tag, Tooltip, Typography } from 'antd'
import dayjs from 'dayjs'

const { Text } = Typography

interface UserIdentityHistoryProps {
  userId: string
}

const UNLINK_REASON_LABELS: Record<string, string> = {
  TenantMigration: 'Tenant Migration',
  AdminRevoked: 'Admin Revoked',
  UserUnlinked: 'User Unlinked',
  ProviderRelinked: 'Provider Relinked',
}

const PROVIDER_LABELS: Record<string, string> = {
  MicrosoftEntraId: 'Microsoft Entra ID',
  Wayd: 'Wayd (Local)',
}

const formatDate = (value?: string | Date | null) =>
  value ? dayjs(value).format('MMM D, YYYY h:mm A') : '—'

const columns: ColumnDef<UserIdentityDto, any>[] = [
  {
    id: 'isActive',
    accessorKey: 'isActive',
    header: 'Status',
    size: 100,
    cell: ({ row }) =>
      row.original.isActive ? (
        <Tag color="success">Active</Tag>
      ) : (
        <Tag>Inactive</Tag>
      ),
  },
  {
    id: 'provider',
    accessorFn: (row) => PROVIDER_LABELS[row.provider] ?? row.provider,
    header: 'Provider',
    size: 160,
    meta: { filterType: 'set' },
  },
  {
    id: 'providerTenantId',
    accessorKey: 'providerTenantId',
    header: 'Tenant',
    size: 260,
    cell: ({ row }) =>
      row.original.providerTenantId ? (
        <Text code copyable={{ text: row.original.providerTenantId }}>
          {row.original.providerTenantId}
        </Text>
      ) : (
        <Text type="secondary">—</Text>
      ),
  },
  {
    id: 'providerSubject',
    accessorKey: 'providerSubject',
    header: 'Subject',
    size: 140,
    cell: ({ row }) => {
      const subject = row.original.providerSubject
      return (
        // Truncated with the full value behind a tooltip and on the clipboard:
        // a provider subject is long, opaque, and only ever needed verbatim.
        <Tooltip title={subject}>
          <Text code copyable={{ text: subject }}>
            {subject.length > 12 ? `${subject.slice(0, 8)}…` : subject}
          </Text>
        </Tooltip>
      )
    },
  },
  {
    id: 'linkedAt',
    accessorKey: 'linkedAt',
    header: 'Linked',
    size: 190,
    cell: ({ row }) => formatDate(row.original.linkedAt),
  },
  {
    id: 'unlinkedAt',
    accessorKey: 'unlinkedAt',
    header: 'Unlinked',
    size: 190,
    cell: ({ row }) => formatDate(row.original.unlinkedAt),
  },
  {
    id: 'unlinkReason',
    accessorFn: (row) =>
      row.unlinkReason
        ? (UNLINK_REASON_LABELS[row.unlinkReason] ?? row.unlinkReason)
        : '—',
    header: 'Reason',
    size: 160,
    meta: { filterType: 'set' },
  },
]

/**
 * Every provider identity this account has been bound to, current and past.
 *
 * A `WaydGrid` rather than a bare antd `Table`, so it sorts, filters and
 * exports like every other table in the product — an audit trail is exactly
 * the sort of thing someone needs to search.
 */
const UserIdentityHistory = ({ userId }: UserIdentityHistoryProps) => {
  const { data, isLoading, error } = useGetUserIdentityHistoryQuery(userId)

  if (error) {
    return <Alert type="error" showIcon title="Failed to load identity history." />
  }

  return (
    <WaydGrid
      variant="simple"
      columns={columns}
      data={data ?? []}
      isLoading={isLoading}
      emptyMessage="No identity history available."
    />
  )
}

export default UserIdentityHistory
