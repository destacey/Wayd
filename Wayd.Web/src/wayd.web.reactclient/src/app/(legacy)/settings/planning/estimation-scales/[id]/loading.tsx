'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function EstimationScaleDetailsLoading() {
  return (
    <>
      <PageTitle title="Estimation Scale" />
      <Skeleton active />
    </>
  )
}
