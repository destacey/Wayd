'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function PortfoliosLoading() {
  return (
    <>
      <PageTitle title="Portfolios" />
      <Skeleton active />
    </>
  )
}
