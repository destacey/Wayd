'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PokerSessionDetailsLoading() {
  return (
    <>
      <PageTitle title="Planning Poker" />
      <Skeleton active />
    </>
  )
}
