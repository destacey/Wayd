'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function RoadmapDetailsLoading() {
  return (
    <>
      <PageTitle title="Roadmap" />
      <Skeleton active />
    </>
  )
}
