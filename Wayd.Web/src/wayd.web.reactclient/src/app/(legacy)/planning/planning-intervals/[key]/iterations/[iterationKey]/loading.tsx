'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PlanningIntervalIterationDetailsLoading() {
  return (
    <>
      <PageTitle title="PI Iteration" />
      <Skeleton active />
    </>
  )
}
