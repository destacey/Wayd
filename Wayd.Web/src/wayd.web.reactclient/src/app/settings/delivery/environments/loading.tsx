'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function DeploymentEnvironmentsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Environments" />
      <Skeleton active />
    </div>
  )
}
