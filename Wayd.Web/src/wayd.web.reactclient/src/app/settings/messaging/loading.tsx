'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function MessagingLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Messaging" />
      <Skeleton active />
    </div>
  )
}
