'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function TeamsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Teams" />
      <Skeleton active />
    </div>
  )
}
