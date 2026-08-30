'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ProductsLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Products" />
      <Skeleton active />
    </div>
  )
}
