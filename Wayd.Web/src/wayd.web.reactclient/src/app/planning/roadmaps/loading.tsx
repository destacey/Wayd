'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RoadmapsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Roadmaps" />
      <Skeleton active />
    </div>
  )
}
