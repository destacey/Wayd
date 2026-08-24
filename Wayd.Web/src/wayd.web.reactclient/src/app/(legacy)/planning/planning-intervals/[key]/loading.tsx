'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PlanningIntervalDetailsLoading() {
  return (
    <>
      <PageTitle title="Planning Interval" />
      <Skeleton active />
    </>
  )
}
