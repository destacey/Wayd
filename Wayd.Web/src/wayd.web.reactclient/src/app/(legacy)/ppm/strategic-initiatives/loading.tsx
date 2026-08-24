'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function StrategicInitiativesLoading() {
  return (
    <>
      <PageTitle title="Strategic Initiatives" />
      <Skeleton active />
    </>
  )
}
