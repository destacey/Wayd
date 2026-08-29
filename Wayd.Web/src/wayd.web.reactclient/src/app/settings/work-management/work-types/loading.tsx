'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function WorkTypesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Work Types" />
      <Skeleton active />
    </div>
  )
}
