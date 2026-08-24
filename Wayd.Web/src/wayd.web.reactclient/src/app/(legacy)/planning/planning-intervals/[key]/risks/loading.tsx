'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PlanningIntervalRisksPageLoading() {
  return (
    <>
      <PageTitle title="PI Risks" />
      <Skeleton active />
    </>
  )
}
