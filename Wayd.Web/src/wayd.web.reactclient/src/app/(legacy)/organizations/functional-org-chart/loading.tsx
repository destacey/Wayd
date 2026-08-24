'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function FunctionalOrganizationChartLoading() {
  return (
    <>
      <PageTitle title="Functional Organization Chart" />
      <Skeleton active />
    </>
  )
}
