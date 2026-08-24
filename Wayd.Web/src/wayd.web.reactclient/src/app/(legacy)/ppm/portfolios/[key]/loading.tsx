'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PortfolioDetailsLoading() {
  return (
    <>
      <PageTitle title="Portfolio" />
      <Skeleton active />
    </>
  )
}
