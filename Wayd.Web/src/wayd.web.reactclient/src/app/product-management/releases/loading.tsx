'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ReleasesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Releases" />
      <Skeleton active />
    </div>
  )
}
