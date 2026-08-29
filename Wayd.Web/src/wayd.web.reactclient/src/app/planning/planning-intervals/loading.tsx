'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PlanningIntervalsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Planning Intervals" />
      <Skeleton active />
    </div>
  )
}
