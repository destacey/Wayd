'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ProjectsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Projects" />
      <Skeleton active />
    </div>
  )
}
