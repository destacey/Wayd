'use client'

import { Skeleton } from 'antd'
import PageTitle from '@/src/components/common/page-title'

export default function ProfileLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Account" />
      <Skeleton active />
    </div>
  )
}
