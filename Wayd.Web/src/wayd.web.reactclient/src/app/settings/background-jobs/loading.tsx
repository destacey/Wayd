'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function BackgroundJobsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Background Jobs" />
      <Skeleton active />
    </div>
  )
}
