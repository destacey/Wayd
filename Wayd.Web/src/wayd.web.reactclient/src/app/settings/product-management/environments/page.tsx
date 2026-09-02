'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { DeploymentEnvironmentDto } from '@/src/services/wayd-api'
import { useGetDeploymentEnvironmentsQuery } from '@/src/store/features/product-management/deployment-environments-api'
import { isApiError } from '@/src/utils'
import { Button } from 'antd'
import { useEffect, useState } from 'react'
import {
  DeploymentEnvironmentForm,
  useDeploymentEnvironmentActions,
} from './_components'

const DeploymentEnvironmentsPage = () => {
  useDocumentTitle('Delivery - Environments')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: environmentData,
    isLoading,
    error,
    refetch,
  } = useGetDeploymentEnvironmentsQuery(undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreateEnvironment = hasPermissionClaim(
    'Permissions.DeploymentEnvironments.Create',
  )

  const { getActionItems, dialogs } = useDeploymentEnvironmentActions({
    onChanged: () => refetch(),
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading deployment environments',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns: ColumnDef<DeploymentEnvironmentDto, any>[] = [
    // First, so it stays put as columns are shown, hidden or reordered around it — the ⋯ is always in
    // the same place regardless of the grid's layout.
    createActionsColumn<DeploymentEnvironmentDto>({
      getItems: getActionItems,
      ariaLabel: 'Deployment environment actions',
    }),
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 220,
      meta: { filterEnableSet: true },
    },
    {
      id: 'category',
      accessorKey: 'category',
      header: 'Category',
      size: 150,
      meta: { filterType: 'set' },
    },
    {
      id: 'ringOrder',
      accessorKey: 'ringOrder',
      header: 'Ring Order',
      size: 120,
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
    {
      id: 'deploymentCount',
      accessorKey: 'deploymentCount',
      header: 'Deployments',
      size: 130,
    },
  ]

  const actions = canCreateEnvironment ? (
    <Button onClick={() => setOpenCreateForm(true)}>Create Environment</Button>
  ) : null

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Environments" actions={actions} />

      <WaydGrid
        columns={columns}
        data={environmentData ?? []}
        onRefresh={refetch}
        isLoading={isLoading}
        persistStateKey="settings-product-management-environments"
        csvFileName="deployment-environments"
        emptyMessage="No deployment environments have been created."
      />

      {dialogs}

      {openCreateForm && (
        <DeploymentEnvironmentForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </div>
  )
}

const DeploymentEnvironmentsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    DeploymentEnvironmentsPage,
    'Permission',
    'Permissions.DeploymentEnvironments.View',
  ),
  'product-management',
)

export default DeploymentEnvironmentsPageWithAuthorization
