'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function DeliveryLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Delivery" />
      <Skeleton active />
    </div>
  )
}
