'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ReleasePackageDetailsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Release Package" />
      <Skeleton active />
    </div>
  )
}
