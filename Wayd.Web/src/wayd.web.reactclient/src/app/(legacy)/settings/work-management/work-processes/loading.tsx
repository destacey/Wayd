'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function WorkProcessesLoading() {
  return (
    <>
      <PageTitle title="Work Processes" />
      <Skeleton active />
    </>
  )
}
