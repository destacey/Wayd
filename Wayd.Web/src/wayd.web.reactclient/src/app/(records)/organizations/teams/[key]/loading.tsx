'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function TeamDetailsLoading() {
  return (
    <>
      <PageTitle title="Team Details" />
      <Skeleton active />
    </>
  )
}
