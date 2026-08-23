'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PlanningIntervalObjectivesPageLoading() {
  return (
    <>
      <PageTitle title="PI Objectives" />
      <Skeleton active />
    </>
  )
}
