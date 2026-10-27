'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function IntakeChannelsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Channels" />
      <Skeleton active />
    </div>
  )
}
