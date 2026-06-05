'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ScoringModelDetailsLoading() {
  return (
    <>
      <PageTitle title="Scoring Model" />
      <Skeleton active />
    </>
  )
}
