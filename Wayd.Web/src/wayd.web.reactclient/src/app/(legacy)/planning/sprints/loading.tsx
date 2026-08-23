'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function SprintsLoading() {
  return (
    <>
      <PageTitle title="Sprints" />
      <Skeleton active />
    </>
  )
}
