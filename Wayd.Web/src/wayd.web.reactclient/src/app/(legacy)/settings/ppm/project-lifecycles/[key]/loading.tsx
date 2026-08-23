'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ProjectLifecycleDetailsLoading() {
  return (
    <>
      <PageTitle title="Project Lifecycle" />
      <Skeleton active />
    </>
  )
}
