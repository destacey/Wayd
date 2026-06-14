'use client'

import { WaydGrid, PageTitle } from '@/src/components/common'
import { RowMenuCellRenderer } from '@/src/components/common/wayd-grid-cell-renderers'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { OidcProviderListItemDto, OidcProviderType } from '@/src/services/wayd-api'
import {
  useGetOidcProvidersQuery,
  useTestOidcProviderDiscoveryMutation,
} from '@/src/store/features/user-management/oidc-providers-api'
import { isApiError } from '@/src/utils'
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
} from '@ant-design/icons'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { Button, Space, Tag, Tooltip, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { useEffect, useState } from 'react'
import {
  CreateOidcProviderForm,
  DeleteOidcProviderForm,
  EditOidcProviderForm,
} from './_components'

const { Text } = Typography

const providerTypeLabel = (type: string) => {
  if (type === OidcProviderType.MicrosoftEntraId) return 'Microsoft Entra ID'
  if (type === OidcProviderType.GenericOidc) return 'Generic OIDC'
  return type
}

interface TestState {
  status: 'idle' | 'loading' | 'success' | 'error'
  issuer?: string
  jwksKeyCount?: number
  error?: string
}

const DiscoveryTestCell = ({
  provider,
  testState,
  onTest,
}: {
  provider: OidcProviderListItemDto
  testState: TestState
  onTest: (id: string) => void
}) => {
  if (testState.status === 'loading') {
    return (
      <Space size={4}>
        <LoadingOutlined style={{ color: 'var(--ant-color-primary)' }} />
        <Text type="secondary" style={{ fontSize: 12 }}>
          Testing...
        </Text>
      </Space>
    )
  }
  if (testState.status === 'success') {
    return (
      <Tooltip
        title={`Issuer: ${testState.issuer} · Keys: ${testState.jwksKeyCount}`}
      >
        <Space size={4}>
          <CheckCircleOutlined style={{ color: 'var(--ant-color-success)' }} />
          <Text style={{ fontSize: 12, color: 'var(--ant-color-success)' }}>
            OK
          </Text>
        </Space>
      </Tooltip>
    )
  }
  if (testState.status === 'error') {
    return (
      <Tooltip title={testState.error}>
        <Space size={4}>
          <CloseCircleOutlined style={{ color: 'var(--ant-color-error)' }} />
          <Text style={{ fontSize: 12, color: 'var(--ant-color-error)' }}>
            Failed
          </Text>
        </Space>
      </Tooltip>
    )
  }
  return (
    <Button size="small" type="link" onClick={() => onTest(provider.id)}>
      Test
    </Button>
  )
}

interface RowMenuProps {
  provider: OidcProviderListItemDto
  canUpdate: boolean
  canDelete: boolean
  onEditClicked: (id: string) => void
  onDeleteClicked: (provider: OidcProviderListItemDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.provider) return []
  const items: ItemType[] = []
  if (props.canUpdate) {
    items.push({
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.provider.id),
    })
  }
  if (props.canDelete) {
    items.push({
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.provider),
    })
  }
  return items
}

const OidcProvidersPage = () => {
  useDocumentTitle('Settings - Identity Providers')
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [editProviderId, setEditProviderId] = useState<string | null>(null)
  const [deleteProvider, setDeleteProvider] =
    useState<OidcProviderListItemDto | null>(null)
  const [testStates, setTestStates] = useState<Record<string, TestState>>({})

  const messageApi = useMessage()
  const { data: providers, isLoading, error, refetch } = useGetOidcProvidersQuery()
  const [testDiscovery] = useTestOidcProviderDiscoveryMutation()

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.OidcProviders.Create')
  const canUpdate = hasPermissionClaim('Permissions.OidcProviders.Update')
  const canDelete = hasPermissionClaim('Permissions.OidcProviders.Delete')
  const showRowMenu = canUpdate || canDelete

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading identity providers.',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const handleTest = async (id: string) => {
    setTestStates((prev) => ({ ...prev, [id]: { status: 'loading' } }))
    try {
      const response = await testDiscovery(id)
      if (response.error) throw response.error
      const result = response.data!
      if (result.success) {
        setTestStates((prev) => ({
          ...prev,
          [id]: {
            status: 'success',
            issuer: result.issuer,
            jwksKeyCount: result.jwksKeyCount,
          },
        }))
      } else {
        setTestStates((prev) => ({
          ...prev,
          [id]: { status: 'error', error: result.error },
        }))
      }
    } catch {
      setTestStates((prev) => ({
        ...prev,
        [id]: { status: 'error', error: 'Request failed' },
      }))
    }
  }

  const handleEdit = (id: string) => setEditProviderId(id)
  const handleDelete = (provider: OidcProviderListItemDto) =>
    setDeleteProvider(provider)

  const columnDefs: ColDef<OidcProviderListItemDto>[] = [
      {
        width: 50,
        filter: false,
        sortable: false,
        resizable: false,
        hide: !showRowMenu,
        suppressHeaderMenuButton: true,
        cellRenderer: (params: ICellRendererParams<OidcProviderListItemDto>) => {
          const menuItems = getRowMenuItems({
            provider: params.data!,
            canUpdate,
            canDelete,
            onEditClicked: handleEdit,
            onDeleteClicked: handleDelete,
          })
          if (menuItems.length === 0) return null
          return RowMenuCellRenderer({ ...params, menuItems })
        },
      },
      { field: 'id', hide: true },
      {
        field: 'name',
        headerName: 'Name',
        flex: 1,
        cellRenderer: (params: ICellRendererParams<OidcProviderListItemDto>) => {
          if (!params.data) return null
          return (
            <Link href={`/settings/auth/providers/${params.data.id}`}>
              {params.data.name}
            </Link>
          )
        },
      },
      { field: 'displayName', headerName: 'Display Name', flex: 1 },
      {
        field: 'providerType',
        headerName: 'Type',
        width: 180,
        valueGetter: (params) => providerTypeLabel(params.data?.providerType ?? ''),
      },
      {
        field: 'authority',
        headerName: 'Authority',
        flex: 2,
      },
      {
        field: 'isEnabled',
        headerName: 'Enabled',
        width: 100,
        cellRenderer: (params: ICellRendererParams<OidcProviderListItemDto>) =>
          params.value ? (
            <Tag color="success">Enabled</Tag>
          ) : (
            <Tag color="default">Disabled</Tag>
          ),
      },
      {
        headerName: 'Discovery',
        width: 120,
        filter: false,
        sortable: false,
        cellRenderer: (params: ICellRendererParams<OidcProviderListItemDto>) => {
          const provider = params.data!
          const testState = testStates[provider.id] ?? { status: 'idle' }
          return (
            <DiscoveryTestCell
              provider={provider}
              testState={testState}
              onTest={handleTest}
            />
          )
        },
      },
  ]

  const refresh = () => { refetch() }

  const actions = !canCreate ? null : (
    <Button onClick={() => setOpenCreateForm(true)}>
      Create Identity Provider
    </Button>
  )

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) refetch()
  }

  const onEditFormClosed = (wasSaved: boolean) => {
    setEditProviderId(null)
    if (wasSaved) refetch()
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setDeleteProvider(null)
    if (wasDeleted) refetch()
  }

  return (
    <>
      <PageTitle title="Identity Providers" actions={actions} />

      <WaydGrid
        height={600}
        columnDefs={columnDefs}
        rowData={providers}
        loadData={refresh}
        loading={isLoading}
      />

      {openCreateForm && (
        <CreateOidcProviderForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
      {editProviderId && (
        <EditOidcProviderForm
          providerId={editProviderId}
          onFormComplete={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {deleteProvider && (
        <DeleteOidcProviderForm
          provider={deleteProvider}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </>
  )
}

const OidcProvidersPageWithAuthorization = authorizePage(
  OidcProvidersPage,
  'Permission',
  'Permissions.OidcProviders.View',
)

export default OidcProvidersPageWithAuthorization
