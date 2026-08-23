'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function EmployeeDetailsLoading() {
  return (
    <>
      <PageTitle title="Employee Details" />
      <Skeleton active />
    </>
  )
}
