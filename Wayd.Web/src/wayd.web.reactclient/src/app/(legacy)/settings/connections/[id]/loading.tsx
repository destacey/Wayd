'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ConnectionDetailsLoading() {
  return (
    <>
      <PageTitle title="Connection Details" />
      <Skeleton active />
    </>
  )
}
