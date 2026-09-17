'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RolloutLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Rollout" />
      <Skeleton active />
    </div>
  )
}
