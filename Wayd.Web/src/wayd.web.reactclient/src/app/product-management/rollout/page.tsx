'use client'

import { PageTitle } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetEnvironmentRolloutQuery } from '@/src/store/features/product-management/deployment-environments-api'
import { Alert, Col, Empty, Row, Skeleton, Switch, Space, Typography } from 'antd'
import { FC, useEffect, useState } from 'react'
import { EnvironmentRolloutCard } from './_components'

const { Text } = Typography

const RolloutPage: FC = () => {
  useDocumentTitle('Delivery - Rollout')
  const [includeInactive, setIncludeInactive] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: rollout,
    isLoading,
    isFetching,
    error,
  } = useGetEnvironmentRolloutQuery({ includeInactive })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load the rollout.')
    }
  }, [error, messageApi])

  const filters = (
    <Space>
      <Switch
        id="include-retired-environments"
        checked={includeInactive}
        onChange={setIncludeInactive}
        loading={isFetching}
      />
      <Text type="secondary">Show retired</Text>
    </Space>
  )

  return (
    <div className="page-gutters">
      <PageTitle title="Rollout" actions={filters} />

      <Alert
        type="info"
        showIcon
        title="What is running, not what happened last"
        description="Each entry is the latest deployment that succeeded and was not rolled back. A failed attempt leaves the version before it in place, so this can differ from the most recent row on the Deployments page — which is the point of having both."
        style={{ marginBottom: 16 }}
      />

      {isLoading ? (
        <Skeleton active />
      ) : (rollout?.length ?? 0) === 0 ? (
        <Empty description="No environments have been defined." />
      ) : (
        <Row gutter={[16, 16]}>
          {rollout?.map((environment) => (
            <Col key={environment.id} xs={24} md={12} xl={8}>
              <EnvironmentRolloutCard environment={environment} />
            </Col>
          ))}
        </Row>
      )}
    </div>
  )
}

const RolloutPageWithAuthorization = requireFeatureFlag(
  authorizePage(RolloutPage, 'Permission', 'Permissions.Delivery.View'),
  'product-management',
)

export default RolloutPageWithAuthorization
