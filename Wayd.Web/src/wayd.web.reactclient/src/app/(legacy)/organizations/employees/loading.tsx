'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function EmployeesLoading() {
  return (
    <>
      <PageTitle title="Employees" />
      <Skeleton active />
    </>
  )
}
