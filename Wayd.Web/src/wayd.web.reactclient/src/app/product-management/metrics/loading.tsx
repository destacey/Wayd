'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function DeliveryMetricsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Delivery Metrics" />
      <Skeleton active />
    </div>
  )
}
