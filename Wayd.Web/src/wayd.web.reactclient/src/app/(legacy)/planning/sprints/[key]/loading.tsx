'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function SprintDetailsLoading() {
  return (
    <>
      <PageTitle title="Sprint" />
      <Skeleton active />
    </>
  )
}
