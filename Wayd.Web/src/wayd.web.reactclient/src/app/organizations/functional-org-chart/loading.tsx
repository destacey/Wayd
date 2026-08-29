'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function FunctionalOrganizationChartLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Functional Organization Chart" />
      <Skeleton active />
    </div>
  )
}
