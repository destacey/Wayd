'use client'

import { FC } from 'react'
import { Alert, App, Button, Flex, Space, Tag, Typography } from 'antd'
import { UserSessionResponse } from '@/src/services/wayd-api'
import {
  useGetMySessionsQuery,
  useRevokeSessionMutation,
  useRevokeAllSessionsMutation,
} from '@/src/store/features/user-management/user-sessions-api'
import {
  WaydGrid,
  createActionsColumn,
  formatDateTime,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'

const { Text } = Typography

const Sessions: FC = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const { logout } = useAuth()

  const { data: sessions, isLoading, error, refetch } = useGetMySessionsQuery()
  const [revokeSession] = useRevokeSessionMutation()
  const [revokeAllSessions] = useRevokeAllSessionsMutation()

  const handleRevoke = (session: UserSessionResponse) => {
    // Revoking your own row signs you out of the page you are standing on, so that case
    // gets a different prompt and routes through logout() rather than the grid action.
    if (session.isCurrent) {
      modal.confirm({
        title: 'Sign out of this device?',
        content:
          'This is the session you are using now. You will be returned to the sign-in page.',
        okText: 'Sign out',
        okButtonProps: { danger: true },
        onOk: () => logout(),
      })
      return
    }

    modal.confirm({
      title: 'Sign out this session?',
      content: `${session.deviceLabel ?? 'This session'} will no longer be able to stay signed in. It may keep working for up to an hour until its current access token expires.`,
      okText: 'Sign out',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await revokeSession(session.id).unwrap()
          messageApi.success('Session signed out.')
        } catch {
          messageApi.error('Unable to sign out that session. Please try again.')
        }
      },
    })
  }

  const handleRevokeAll = () => {
    modal.confirm({
      title: 'Sign out of all devices?',
      content:
        'Every session, including this one, will be signed out. You will be returned to the sign-in page; other devices may keep working for up to an hour until their access tokens expire.',
      okText: 'Sign out everywhere',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await revokeAllSessions().unwrap()
          // The call revoked this session too, so finish locally rather than leaving the
          // user in a UI whose credentials the server has already discarded.
          await logout()
        } catch {
          messageApi.error('Unable to sign out all devices. Please try again.')
        }
      },
    })
  }

  const columns: ColumnDef<UserSessionResponse>[] = [
    createActionsColumn<UserSessionResponse>({
      ariaLabel: 'Session actions',
      getItems: (session): ItemType[] => [
        {
          key: 'revoke',
          label: session.isCurrent ? 'Sign out' : 'Sign out this session',
          danger: true,
          onClick: () => handleRevoke(session),
        },
      ],
    }),
    {
      id: 'deviceLabel',
      accessorKey: 'deviceLabel',
      header: 'Device',
      size: 320,
      cell: ({ row }) => (
        <Space>
          <Text>{row.original.deviceLabel ?? 'Unknown device'}</Text>
          {row.original.isCurrent && <Tag color="success">This device</Tag>}
        </Space>
      ),
    },
    {
      id: 'ipAddress',
      accessorKey: 'ipAddress',
      header: 'IP address',
      size: 160,
      cell: ({ getValue }) => (getValue() as string) ?? '—',
    },
    {
      id: 'lastUsedAt',
      accessorKey: 'lastUsedAt',
      header: 'Last active',
      size: 180,
      meta: { columnType: 'dateTime' },
      cell: ({ getValue }) => formatDateTime(getValue()),
    },
    {
      id: 'createdAt',
      accessorKey: 'createdAt',
      header: 'Signed in',
      size: 180,
      meta: { columnType: 'dateTime' },
      cell: ({ getValue }) => formatDateTime(getValue()),
    },
  ]

  if (error) {
    return (
      <Alert
        type="error"
        title="Unable to load sessions"
        description="Please refresh the page to try again."
      />
    )
  }

  return (
    <Flex vertical gap="middle">
      <Alert
        type="info"
        showIcon
        title="Personal access tokens are managed separately"
        description="Signing out of a device does not revoke personal access tokens. Review those on the PATs tab."
      />

      <Flex justify="space-between" align="center">
        <Text type="secondary">
          Devices currently signed in to your account. Sign out any you do not
          recognise — a signed-out device can take up to an hour to lose access.
        </Text>
        <Button danger onClick={handleRevokeAll} disabled={!sessions?.length}>
          Sign out of all devices
        </Button>
      </Flex>

      <WaydGrid
        columns={columns}
        data={sessions ?? []}
        isLoading={isLoading}
        onRefresh={refetch}
        persistStateKey="account-sessions"
        csvFileName="sessions"
        emptyMessage="No active sessions found."
      />
    </Flex>
  )
}

export default Sessions
