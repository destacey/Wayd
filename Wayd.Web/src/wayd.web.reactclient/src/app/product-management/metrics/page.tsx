'use client'

import { PageTitle } from '@/src/components/common'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetDeliveryMetricsQuery } from '@/src/store/features/product-management/delivery-metrics-api'
import { useGetDeploymentsQuery } from '@/src/store/features/product-management/deployments-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { DatePicker, Flex, Select, Skeleton, Space } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC, useEffect, useState } from 'react'
import { DeploymentsGrid } from '../deployments/_components'
import { DeliveryMetricTiles, UnavailableMetrics } from './_components'

const { RangePicker } = DatePicker

/** The window the page opens on. Long enough that a weekly cadence shows several points. */
const defaultRange: [Dayjs, Dayjs] = [
  dayjs().subtract(90, 'day').startOf('day'),
  dayjs().endOf('day'),
]

const DeliveryMetricsPage: FC = () => {
  useDocumentTitle('Delivery Metrics')
  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange)
  const [productId, setProductId] = useState<string | undefined>()

  const messageApi = useMessage()

  const {
    data: metrics,
    isLoading,
    error,
  } = useGetDeliveryMetricsQuery({
    from: range[0].toISOString(),
    to: range[1].toISOString(),
    productId,
  })

  // The same window the measures cover, so a reader can see the deployments behind the numbers rather
  // than taking them on trust. Not filtered to production: the grid is where you check what the
  // production-only measures left out.
  const { data: deployments, isLoading: deploymentsLoading } =
    useGetDeploymentsQuery({ startedOnOrAfter: range[0].toISOString() })

  const { data: products } = useGetProductsQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load delivery metrics.')
    }
  }, [error, messageApi])

  const productOptions = (products ?? [])
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const filters = (
    <Space wrap>
      <RangePicker
        value={range}
        allowClear={false}
        onChange={(values) => {
          if (values?.[0] && values[1]) {
            setRange([values[0], values[1]])
          }
        }}
      />
      <Select
        style={{ width: 220 }}
        placeholder="All products"
        options={productOptions}
        value={productId}
        onChange={setProductId}
        allowClear
        showSearch
        optionFilterProp="label"
      />
    </Space>
  )

  return (
    <div className="page-gutters">
      <PageTitle title="Delivery Metrics" />
      <Flex vertical gap="middle">
        {filters}

        {isLoading || !metrics ? (
          <Skeleton active />
        ) : (
          <>
            <DeliveryMetricTiles
              deploymentFrequency={metrics.deploymentFrequency}
              changeFailureRate={metrics.changeFailureRate}
            />
            <UnavailableMetrics unavailable={metrics.unavailable ?? []} />
          </>
        )}

        <DeploymentsGrid
          deployments={deployments ?? []}
          isLoading={deploymentsLoading}
          persistStateKey="product-management-delivery-metrics-deployments"
          emptyMessage="No deployments started in this window."
        />
      </Flex>
    </div>
  )
}

const DeliveryMetricsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    DeliveryMetricsPage,
    'Permission',
    'Permissions.DeliveryMetrics.View',
  ),
  'product-management',
)

export default DeliveryMetricsPageWithAuthorization
