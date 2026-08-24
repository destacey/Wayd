'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function WorkItemDetailsLoading() {
  return (
    <>
      <PageTitle title="Work Item" />
      <Skeleton active />
    </>
  )
}
