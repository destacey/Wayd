'use client'

import { useState, FC, useMemo } from 'react'
import { Button, Tag, Alert, Flex, Typography, App, Space } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import {
  useGetMyPersonalAccessTokensQuery,
  useRevokePersonalAccessTokenMutation,
  useDeletePersonalAccessTokenMutation,
} from '@/src/store/features/user-management/personal-access-tokens-api'
import { PersonalAccessTokenDto } from '@/src/services/wayd-api'
import {
  WaydGrid,
  createActionsColumn,
  formatDateTime,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@tanstack/react-table'
import {
  CreatePersonalAccessTokenForm,
  EditPersonalAccessTokenForm,
  PersonalAccessTokenCreatedModal,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { ItemType } from 'antd/es/menu/interface'

const { Text } = Typography

const tokenStatus = (token: PersonalAccessTokenDto): string => {
  if (token.isRevoked) return 'Revoked'
  if (token.isExpired) return 'Expired'
  if (token.isActive) return 'Active'
  return 'Unknown'
}

const statusTagColor: Record<string, string | undefined> = {
  Revoked: 'error',
  Expired: 'warning',
  Active: 'success',
}

/** Renders a dateTime cell in the grid's standard format, or "Never" when unset. */
const renderDateTimeOrNever = (value: unknown) =>
  value ? formatDateTime(value) : 'Never'

const PersonalAccessTokens: FC = () => {
  const [isCreateFormVisible, setIsCreateFormVisible] = useState(false)
  const [isEditFormVisible, setIsEditFormVisible] = useState(false)
  const [editingToken, setEditingToken] =
    useState<PersonalAccessTokenDto | null>(null)
  const [newToken, setNewToken] = useState<string | null>(null)
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const {
    data: tokens,
    isLoading,
    error,
    refetch,
  } = useGetMyPersonalAccessTokensQuery()
  const [revokeToken] = useRevokePersonalAccessTokenMutation()
  const [deleteToken] = useDeletePersonalAccessTokenMutation()

  const handleFormCreate = (token: string) => {
    setNewToken(token)
  }

  const handleFormCancel = () => {
    setIsCreateFormVisible(false)
  }

  const handleEditFormUpdate = () => {
    setIsEditFormVisible(false)
    setEditingToken(null)
  }

  const handleEditFormCancel = () => {
    setIsEditFormVisible(false)
    setEditingToken(null)
  }

  const handleTokenModalClose = () => {
    setNewToken(null)
    setIsCreateFormVisible(false)
  }

  const refresh = async () => {
    refetch()
  }

  const columns = useMemo<ColumnDef<PersonalAccessTokenDto, any>[]>(() => {
    const handleEdit = (token: PersonalAccessTokenDto) => {
      setEditingToken(token)
      setIsEditFormVisible(true)
    }

    const handleRevoke = async (id: string, name: string) => {
      try {
        await revokeToken(id).unwrap()
        messageApi.success(`Token "${name}" revoked successfully`)
      } catch (error) {
        console.error('Failed to revoke token:', error)
        messageApi.error('Failed to revoke token')
      }
    }

    const handleDelete = async (id: string, name: string) => {
      try {
        await deleteToken(id).unwrap()
        messageApi.success(`Token "${name}" deleted successfully`)
      } catch (error) {
        console.error('Failed to delete token:', error)
        messageApi.error('Failed to delete token')
      }
    }

    return [
      createActionsColumn<PersonalAccessTokenDto>({
        ariaLabel: 'Token actions',
        getItems: (token) => {
          if (!token.id) return []

          const items: ItemType[] = []

          if (token.isActive) {
            items.push({
              key: 'editToken',
              label: 'Edit',
              onClick: () => handleEdit(token),
            })
            items.push({
              key: 'revokeToken',
              label: 'Revoke',
              onClick: () => {
                modal.confirm({
                  title: `Revoke Token - ${token.name}`,
                  content:
                    'Are you sure you want to revoke this token? It will no longer work.',
                  okText: 'Revoke',
                  okType: 'danger',
                  onOk: () => handleRevoke(token.id, token.name),
                })
              },
            })
          }

          items.push({
            key: 'deleteToken',
            label: 'Delete',
            onClick: () => {
              modal.confirm({
                title: `Delete Token - ${token.name}`,
                content:
                  'Are you sure you want to permanently delete this token? This cannot be undone.',
                okText: 'Delete',
                okType: 'danger',
                onOk: () => handleDelete(token.id, token.name),
              })
            },
          })

          return items
        },
      }),
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
      },
      {
        id: 'status',
        accessorFn: (row) => tokenStatus(row),
        header: 'Status',
        size: 120,
        meta: { filterType: 'set' },
        cell: ({ getValue }) => {
          const status = getValue() as string
          return <Tag color={statusTagColor[status]}>{status}</Tag>
        },
      },
      {
        id: 'expiresAt',
        accessorKey: 'expiresAt',
        header: 'Expires',
        size: 180,
        meta: { columnType: 'dateTime' },
        cell: ({ getValue }) => renderDateTimeOrNever(getValue()),
      },
      {
        id: 'lastUsedAt',
        accessorKey: 'lastUsedAt',
        header: 'Last Used',
        size: 180,
        meta: {
          columnType: 'dateTime',
          headerTooltip:
            'The last time this token was used for authentication. Updates at most once per hour.',
        },
        cell: ({ getValue }) => renderDateTimeOrNever(getValue()),
      },
    ]
  }, [modal, revokeToken, deleteToken, messageApi])

  if (error) {
    return (
      <Alert
        title="Error"
        description="Failed to load personal access tokens"
        type="error"
        showIcon
      />
    )
  }

  return (
    <Flex vertical gap={16}>
      <Space vertical size={16}>
        <Alert
          title="Personal Access Tokens (PATs)"
          description="Personal access tokens function like passwords for API authentication. Keep them secure and never share them. Each user can have up to 10 active tokens."
          type="info"
          showIcon
        />

        <Text type="secondary">
          Use this token in the <code>x-api-key</code> header when making API
          requests.
        </Text>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => setIsCreateFormVisible(true)}
        >
          Create New Token
        </Button>
      </Space>

      <WaydGrid
        height={500}
        columns={columns}
        data={tokens ?? []}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="personal-access-tokens"
        emptyMessage="No PATs found."
      />

      {isCreateFormVisible && (
        <CreatePersonalAccessTokenForm
          onFormCreate={handleFormCreate}
          onFormCancel={handleFormCancel}
        />
      )}

      {isEditFormVisible && editingToken && (
        <EditPersonalAccessTokenForm
          token={editingToken}
          onFormUpdate={handleEditFormUpdate}
          onFormCancel={handleEditFormCancel}
        />
      )}

      {newToken && (
        <PersonalAccessTokenCreatedModal
          token={newToken}
          onClose={handleTokenModalClose}
        />
      )}
    </Flex>
  )
}

export default PersonalAccessTokens
