'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function ExpenditureCategorieDetailsLoading() {
  return (
    <>
      <PageTitle title="Expenditure Category" />
      <Skeleton active />
    </>
  )
}
