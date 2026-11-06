'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RequestTagsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Request Tags" />
      <Skeleton active />
    </div>
  )
}
