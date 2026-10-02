'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function SprintsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Sprints" />
      <Skeleton active />
    </div>
  )
}
