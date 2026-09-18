'use client'

import { PageTitle } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetDeliveryOverviewQuery,
  useGetRecentDeliveryEventsQuery,
} from '@/src/store/features/product-management/delivery-overview-api'
import { Card, Col, Empty, Row, Select, Skeleton, Space, Typography } from 'antd'
import { RightOutlined } from '@ant-design/icons'
import Link from 'next/link'
import dayjs from 'dayjs'
import { FC, useEffect, useState } from 'react'
import { ProductTreeSelect } from '../_components'
import {
  DeliveryOverviewTiles,
  RecentDeliveryActivity,
  VersionActivity,
  VersionActivityLegend,
} from './_components'

const { Text } = Typography

/** Windows short enough that the heatmap stays one cell per day at a readable size. */
const windowOptions = [
  { value: 14, label: 'Last 14 days' },
  { value: 30, label: 'Last 30 days' },
  { value: 90, label: 'Last 90 days' },
]

const DeliveryPage: FC = () => {
  useDocumentTitle('Delivery')
  const [windowDays, setWindowDays] = useState<number>(14)
  const [productId, setProductId] = useState<string | undefined>()

  const messageApi = useMessage()

  const to = dayjs().format('YYYY-MM-DD')
  const from = dayjs()
    .subtract(windowDays - 1, 'day')
    .format('YYYY-MM-DD')

  const {
    data: overview,
    isLoading,
    error,
  } = useGetDeliveryOverviewQuery({ from, to, productId })

  // Not bounded by the window: the feed answers "what just happened", and an empty fortnight should
  // still show the last thing that did.
  const { data: recent } = useGetRecentDeliveryEventsQuery({ take: 8, productId })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load delivery activity.')
    }
  }, [error, messageApi])

  const filters = (
    <Space wrap>
      {/* Wide enough for a nested product name at depth, since the trigger shows the leaf on its
          own without the branch that disambiguates it. */}
      <div style={{ width: 340 }}>
        <ProductTreeSelect
          id="delivery-scope"
          value={productId}
          onChange={setProductId}
          placeholder="All products"
          // Any node, including a grouping: the page scopes to the node and everything beneath it, so
          // picking a product line reports its releasable products rather than nothing. Cutting a
          // version is what needs a releasable node, and this is a filter, not a target.
          selectable="all"
        />
      </div>
      <Select
        id="delivery-window"
        value={windowDays}
        onChange={setWindowDays}
        options={windowOptions}
      />
    </Space>
  )

  // Selecting a node covers it and everything beneath it, so a grouping reports its children rather
  // than reporting nothing — which is what the count describes.
  const subtitle = overview
    ? `${overview.scope.product?.name ?? 'All products'} · ${
        overview.scope.releasableNodeCount
      } releasable node${overview.scope.releasableNodeCount === 1 ? '' : 's'}`
    : undefined

  return (
    <div className="page-gutters">
      <PageTitle
        title="Delivery"
        subtitle={subtitle}
        actions={filters}
        tooltip="Versions cut and released over the window, for a product and everything beneath it. Measures what was shipped, not what reached an environment — deployments and the DORA measures are on Delivery Metrics."
      />

      {isLoading || !overview ? (
        <Skeleton active />
      ) : (
        <Space orientation="vertical" size={16} style={{ width: '100%' }}>
          <DeliveryOverviewTiles
            frequency={overview.frequency}
            cutToReleased={overview.cutToReleased}
          />

          {/* Side by side from xl, where there is room for the grid to stay readable beside the
              feed. Below that they stack, and the grid takes the full width rather than being
              squeezed into a column too narrow to show a fortnight. */}
          <Row gutter={[16, 16]}>
            <Col xs={24} xl={17}>
              <Card
                title="Version activity"
                size="small"
                style={{ height: '100%' }}
                // The rule under the header competes with the grid's own rows for the eye.
                styles={{ header: { borderBottom: 'none' } }}
                // Only alongside a grid is there something to read it against.
                extra={
                  overview.frequency.count > 0 ? (
                    <VersionActivityLegend />
                  ) : null
                }
              >
                {overview.frequency.count === 0 ? (
                  <Empty description="No versions were released in this window." />
                ) : (
                  <VersionActivity
                    activity={overview.activity}
                    from={from}
                    to={to}
                  />
                )}
              </Card>
            </Col>
            <Col xs={24} xl={7}>
              {/* Sized to its content rather than stretched to the grid's height: the feed is a
                  fixed handful of entries, and matching a tall grid would leave it mostly empty.
                  The floor stops it looking stunted on a quiet week. */}
              <Card
                title="Recent delivery activity"
                size="small"
                style={{ minHeight: 300 }}
                styles={{ header: { borderBottom: 'none' } }}
                extra={
                  <Link href="/product-management/versions">
                    <Text type="secondary" style={{ fontSize: 12 }}>
                      View all <RightOutlined style={{ fontSize: 10 }} />
                    </Text>
                  </Link>
                }
              >
                <RecentDeliveryActivity events={recent ?? []} />
              </Card>
            </Col>
          </Row>
        </Space>
      )}
    </div>
  )
}

const DeliveryPageWithAuthorization = requireFeatureFlag(
  authorizePage(DeliveryPage, 'Permission', 'Permissions.Delivery.View'),
  'product-management',
)

export default DeliveryPageWithAuthorization
