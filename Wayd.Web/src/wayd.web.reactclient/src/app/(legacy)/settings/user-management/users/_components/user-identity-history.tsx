'use client'

import { Alert, Empty, Spin, Table, Tag, Tooltip, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { UserIdentityDto } from '@/src/services/wayd-api'
import { useGetUserIdentityHistoryQuery } from '@/src/store/features/user-management/users-api'

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

const formatDate = (value?: string | null) =>
  value ? dayjs(value).format('MMM D, YYYY h:mm A') : '—'

const columns: ColumnsType<UserIdentityDto> = [
  {
    title: 'Status',
    dataIndex: 'isActive',
    key: 'isActive',
    width: 100,
    render: (isActive: boolean) =>
      isActive ? (
        <Tag color="success">Active</Tag>
      ) : (
        <Tag>Inactive</Tag>
      ),
  },
  {
    title: 'Provider',
    dataIndex: 'provider',
    key: 'provider',
    render: (provider: string) => PROVIDER_LABELS[provider] ?? provider,
  },
  {
    title: 'Tenant',
    dataIndex: 'providerTenantId',
    key: 'providerTenantId',
    render: (tenantId?: string | null) =>
      tenantId ? (
        <Text code copyable={{ text: tenantId }}>
          {tenantId}
        </Text>
      ) : (
        <Text type="secondary">—</Text>
      ),
  },
  {
    title: 'Subject',
    dataIndex: 'providerSubject',
    key: 'providerSubject',
    render: (subject: string) => (
      <Tooltip title={subject}>
        <Text code copyable={{ text: subject }}>
          {subject.length > 12 ? `${subject.slice(0, 8)}…` : subject}
        </Text>
      </Tooltip>
    ),
  },
  {
    title: 'Linked',
    dataIndex: 'linkedAt',
    key: 'linkedAt',
    render: formatDate,
  },
  {
    title: 'Unlinked',
    dataIndex: 'unlinkedAt',
    key: 'unlinkedAt',
    render: formatDate,
  },
  {
    title: 'Reason',
    dataIndex: 'unlinkReason',
    key: 'unlinkReason',
    render: (reason?: string | null) =>
      reason ? UNLINK_REASON_LABELS[reason] ?? reason : '—',
  },
]

const UserIdentityHistory = ({ userId }: UserIdentityHistoryProps) => {
  const { data, isLoading, error } = useGetUserIdentityHistoryQuery(userId)

  if (isLoading) {
    return <Spin />
  }

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="Failed to load identity history."
      />
    )
  }

  if (!data || data.length === 0) {
    return <Empty description="No identity history available." />
  }

  return (
    <Table<UserIdentityDto>
      rowKey="id"
      size="small"
      columns={columns}
      dataSource={data}
      pagination={false}
    />
  )
}

export default UserIdentityHistory
