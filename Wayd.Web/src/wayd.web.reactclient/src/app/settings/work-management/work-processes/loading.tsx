'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function WorkProcessesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Work Processes" />
      <Skeleton active />
    </div>
  )
}
