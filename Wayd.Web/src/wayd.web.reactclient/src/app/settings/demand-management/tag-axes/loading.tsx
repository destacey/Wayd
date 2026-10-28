'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function TagAxesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Tag Axes" />
      <Skeleton active />
    </div>
  )
}
