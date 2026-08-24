'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function StrategicThemeDetailsLoading() {
  return (
    <>
      <PageTitle title="Strategic Theme" />
      <Skeleton active />
    </>
  )
}
