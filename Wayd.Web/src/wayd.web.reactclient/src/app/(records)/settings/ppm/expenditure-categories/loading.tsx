'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ExpenditureCategoriesLoading() {
  return (
    <>
      <PageTitle title="Expenditure Categories" />
      <Skeleton active />
    </>
  )
}
