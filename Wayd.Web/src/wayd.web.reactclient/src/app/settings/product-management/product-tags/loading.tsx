'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ProductTagCategoriesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Product Tags" />
      <Skeleton active />
    </div>
  )
}
