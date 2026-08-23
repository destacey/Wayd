'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function StoryMapDetailsLoading() {
  return (
    <>
      <PageTitle title="Story Map" />
      <Skeleton active />
    </>
  )
}
