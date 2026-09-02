'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ReleasesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Versions" />
      <Skeleton active />
    </div>
  )
}
