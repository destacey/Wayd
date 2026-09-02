'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ReleasePackagesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Release Packages" />
      <Skeleton active />
    </div>
  )
}
