'use client'

import { PageTitle } from '@/src/components/common'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { EnvironmentCategory } from '@/src/services/wayd-api'
import { useGetDeploymentEnvironmentsQuery } from '@/src/store/features/product-management/deployment-environments-api'
import { useGetDeploymentsQuery } from '@/src/store/features/product-management/deployments-api'
import { Button, DatePicker, Flex, Select, Space } from 'antd'
import { Dayjs } from 'dayjs'
import { FC, useEffect, useState } from 'react'
import { DeploymentsGrid, StartDeploymentForm } from './_components'

/**
 * The wire value for each category.
 *
 * The API binds this parameter as an int, while the generated client models the enum as its names, so
 * the number has to be supplied here. Written out rather than derived from the enum's declaration
 * order: the backing values start at 1, so anything positional is off by one on every category and
 * quietly filters to the wrong one.
 */
const environmentCategoryValue: Record<EnvironmentCategory, number> = {
  [EnvironmentCategory.Development]: 1,
  [EnvironmentCategory.Testing]: 2,
  [EnvironmentCategory.Staging]: 3,
  [EnvironmentCategory.Production]: 4,
}

const DeploymentsPage: FC = () => {
  useDocumentTitle('Deployments')
  const [openStartForm, setOpenStartForm] = useState<boolean>(false)
  const [environmentId, setEnvironmentId] = useState<string | undefined>()
  const [environmentCategory, setEnvironmentCategory] = useState<
    EnvironmentCategory | undefined
  >()
  const [startedOnOrAfter, setStartedOnOrAfter] = useState<Dayjs | null>(null)

  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateDeployment = hasPermissionClaim('Permissions.Delivery.Create')

  // Filtered server-side rather than in the grid: the deployment record grows without bound, and the
  // date filter in particular is what keeps a long-lived environment's history from being fetched
  // whole.
  const {
    data: deploymentData,
    isLoading,
    error,
    refetch,
  } = useGetDeploymentsQuery({
    environmentId,
    environmentCategory: environmentCategory
      ? environmentCategoryValue[environmentCategory]
      : undefined,
    startedOnOrAfter: startedOnOrAfter?.toISOString(),
  })

  // Every environment, not just the active ones: a retired environment still has deployments worth
  // filtering to.
  const { data: environments } = useGetDeploymentEnvironmentsQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load deployments.')
    }
  }, [error, messageApi])

  const actions = !canCreateDeployment ? null : (
    <Button onClick={() => setOpenStartForm(true)}>Start Deployment</Button>
  )

  const onStartFormClosed = (wasStarted: boolean) => {
    setOpenStartForm(false)
    if (wasStarted) {
      refetch()
    }
  }

  const environmentOptions = (environments ?? [])
    .map((environment) => ({
      value: environment.id,
      label: environment.name,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const filters = (
    <Space wrap>
      <Select
        style={{ width: 200 }}
        placeholder="Environment"
        options={environmentOptions}
        value={environmentId}
        onChange={setEnvironmentId}
        allowClear
        showSearch
        optionFilterProp="label"
      />
      <Select
        style={{ width: 160 }}
        placeholder="Category"
        options={Object.values(EnvironmentCategory).map((category) => ({
          value: category,
          label: category,
        }))}
        value={environmentCategory}
        onChange={setEnvironmentCategory}
        allowClear
      />
      <DatePicker
        placeholder="Started on or after"
        value={startedOnOrAfter}
        onChange={setStartedOnOrAfter}
      />
    </Space>
  )

  return (
    <div className="page-gutters">
      <PageTitle title="Deployments" actions={actions} />
      <Flex vertical gap="small">
        {filters}
        <DeploymentsGrid
          deployments={deploymentData ?? []}
          isLoading={isLoading}
          refetch={refetch}
          persistStateKey="product-management-deployments"
        />
      </Flex>
      {openStartForm && (
        <StartDeploymentForm
          onFormComplete={() => onStartFormClosed(true)}
          onFormCancel={() => onStartFormClosed(false)}
        />
      )}
    </div>
  )
}

const DeploymentsPageWithAuthorization = requireFeatureFlag(
  authorizePage(DeploymentsPage, 'Permission', 'Permissions.Delivery.View'),
  'product-management',
)

export default DeploymentsPageWithAuthorization
