'use client'

import PageTitle from '@/src/components/common/page-title'
import { Skeleton } from 'antd'

export default function EmployeesLoading() {
  return (
    <div className="page-gutters">
      <PageTitle title="Employees" />
      <Skeleton active />
    </div>
  )
}
