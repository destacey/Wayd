'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function StrategicInitiativeDetailsLoading() {
  return (
    <>
      <PageTitle title="Strategic Initiative" />
      <Skeleton active />
    </>
  )
}
